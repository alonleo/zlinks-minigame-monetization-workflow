using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP 请求管理器：封装短链请求，提供统一的对外入口。
/// 纯 C# 单例（同 PlatformManager / SidebarManager），内部用隐藏 GameObject 驱动协程。
///
/// 网络优化策略：
///   1. 并发上限  —— 超出 MaxConcurrent 的请求排队，避免瞬间打满连接
///   2. 超时兜底  —— 每个请求独立超时，UnityWebRequest.timeout + 手动 deadline 双保险
///   3. 失败重试  —— 5xx / 超时 / 网络错误指数退避重试，4xx 不重试
///   4. GET 去重  —— 相同 URL 的在途 GET 合并回调，减少重复请求
///   5. 统一 header / 超时 / 重试配置，调用方无需关心底层细节
/// </summary>
public class HttpManager
{
    public static HttpManager Instance { get; private set; } = new HttpManager();

    /// <summary>检查 Instance 是否存在</summary>
    public static bool HasInstance => Instance != null;

    // ==================== 可调参数 ====================

    /// <summary>最大并发请求数，超出部分排队</summary>
    public int MaxConcurrent = 4;

    /// <summary>默认超时（秒）</summary>
    public float DefaultTimeout = 10f;

    /// <summary>最大重试次数（仅对可重试错误：5xx / 超时 / 网络错误）</summary>
    public int MaxRetries = 3;

    /// <summary>重试指数退避基数（秒），第 n 次重试延迟 = RetryBaseDelay * 2^n</summary>
    public float RetryBaseDelay = 1f;

    // ==================== 内部状态 ====================

    private HttpManagerRunner _runner;
    private readonly Queue<HttpRequestTask> _pending = new Queue<HttpRequestTask>();
    private readonly List<HttpRequestTask> _running = new List<HttpRequestTask>();

    /// <summary>GET 去重：url → 在途 task，相同 URL 的后续 GET 合并回调</summary>
    private readonly Dictionary<string, HttpRequestTask> _inflightGet =
        new Dictionary<string, HttpRequestTask>();

    private HttpManager() { }

    /// <summary>显式初始化，提前创建协程驱动器。也可不调用，首次请求时自动初始化。</summary>
    public void Init()
    {
        EnsureRunner();
    }

    private void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("[HttpManagerRunner]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<HttpManagerRunner>();
    }

    // ==================== 公开 API ====================

    /// <summary>发送 GET 请求。相同 URL 的在途 GET 会自动合并回调。</summary>
    /// <param name="url">请求地址（短链）</param>
    /// <param name="callback">回调(success, responseText)，主线程调用</param>
    /// <param name="timeout">超时秒数，&lt;0 用 DefaultTimeout</param>
    public void SendGet(string url, Action<bool, string> callback, float timeout = -1f)
    {
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(false, "url is empty");
            return;
        }

        EnsureRunner();

        // GET 去重：相同 URL 在途则合并回调
        if (_inflightGet.TryGetValue(url, out var existing))
        {
            existing.AddCallback(callback);
            return;
        }

        var task = new HttpRequestTask
        {
            Url = url,
            Method = "GET",
            Timeout = timeout < 0 ? DefaultTimeout : timeout,
        };
        task.AddCallback(callback);
        _inflightGet[url] = task;
        Enqueue(task);
    }

    /// <summary>发送 POST 请求（JSON body）。</summary>
    public void SendPost(string url, string jsonBody, Action<bool, string> callback, float timeout = -1f)
    {
        SendPost(url, jsonBody, "application/json", callback, timeout);
    }

    /// <summary>发送 POST 请求（自定义 Content-Type）。</summary>
    public void SendPost(string url, string body, string contentType,
        Action<bool, string> callback, float timeout = -1f)
    {
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(false, "url is empty");
            return;
        }

        EnsureRunner();

        var task = new HttpRequestTask
        {
            Url = url,
            Method = "POST",
            Body = body,
            ContentType = contentType,
            Timeout = timeout < 0 ? DefaultTimeout : timeout,
        };
        task.AddCallback(callback);
        Enqueue(task);
    }

    /// <summary>发送 POST 请求（表单 x-www-form-urlencoded）。</summary>
    public void SendPostForm(string url, Dictionary<string, string> form,
        Action<bool, string> callback, float timeout = -1f)
    {
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(false, "url is empty");
            return;
        }

        EnsureRunner();

        var task = new HttpRequestTask
        {
            Url = url,
            Method = "POST_FORM",
            FormFields = form,
            Timeout = timeout < 0 ? DefaultTimeout : timeout,
        };
        task.AddCallback(callback);
        Enqueue(task);
    }

    // Owner-scoped cancellation keeps unrelated game HTTP requests intact.
    public void SendPostOwned(object owner, string url, string body, Action<bool,string> callback)
    {
        EnsureRunner();
        var task=new HttpRequestTask { Owner=owner, Url=url, Method="POST", Body=body, ContentType="application/json", Timeout=DefaultTimeout };
        task.AddCallback(callback); Enqueue(task);
    }
    public void CancelOwner(object owner)
    {
        foreach(var task in _pending) if(ReferenceEquals(task.Owner,owner)) task.Cancelled=true;
        foreach(var task in _running) if(ReferenceEquals(task.Owner,owner)) { task.Cancelled=true; task.ActiveRequest?.Abort(); }
    }

    // ==================== 内部调度 ====================

    private void Enqueue(HttpRequestTask task)
    {
        _pending.Enqueue(task);
        Pump();
    }

    /// <summary>尝试从待发队列取出请求并发，直到并发满或队列空。</summary>
    private void Pump()
    {
        while (_running.Count < MaxConcurrent && _pending.Count > 0)
        {
            var task = _pending.Dequeue();
            if (task.Cancelled) continue;
            _running.Add(task);
            _runner.StartCoroutine(RunRequest(task));
        }
    }

    /// <summary>执行单个请求（含重试循环）。</summary>
    private IEnumerator RunRequest(HttpRequestTask task)
    {
        int attempt = 0;
        while (true)
        {
            if (task.Cancelled) { FinishTask(task,false,"cancelled"); yield break; }
            using (UnityWebRequest req = CreateRequest(task))
            {
                task.ActiveRequest=req;
                // UnityWebRequest.timeout 原生超时（秒）
                req.timeout = Mathf.Max(1, Mathf.CeilToInt(task.Timeout));

                req.SendWebRequest();

                // 手动 deadline 兜底：WebGL 下原生 timeout 偶有不触发，额外 +2s 宽限
                float deadline = Time.realtimeSinceStartup + task.Timeout + 2f;
                while (!req.isDone)
                {
                    if (task.Cancelled || Time.realtimeSinceStartup > deadline)
                    {
                        req.Abort();
                        break;
                    }
                    yield return null;
                }

                bool success = false;
                string result = null;
                bool retryable = false;

                if (req.isDone && req.result == UnityWebRequest.Result.Success)
                {
                    success = true;
                    result = req.downloadHandler != null ? req.downloadHandler.text : "";
                }
                else
                {
                    result = req.error ?? (req.isDone ? "request failed" : "timeout");

                    // 5xx / 超时 / 连接错误 → 可重试；4xx / 协议错误 → 不重试
                    long code = req.responseCode;
                    retryable = code >= 500
                        || code == 0
                        || req.result == UnityWebRequest.Result.ConnectionError
                        || !req.isDone; // 被手动 deadline 中断
                }

                task.ActiveRequest=null;
                if (task.Cancelled || success || !retryable || attempt >= MaxRetries)
                {
                    FinishTask(task, success, result);
                    yield break;
                }
            }

            // 指数退避：第 n 次重试延迟 = RetryBaseDelay * 2^(n-1)
            float delay = RetryBaseDelay * Mathf.Pow(2f, attempt);
            attempt++;
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    /// <summary>请求结束：清理状态、回调、驱动队列。</summary>
    private void FinishTask(HttpRequestTask task, bool success, string result)
    {
        _running.Remove(task);
        if (task.Method == "GET")
        {
            _inflightGet.Remove(task.Url);
        }
        if (!task.Cancelled) task.Invoke(success, result);
        Pump();
    }

    private UnityWebRequest CreateRequest(HttpRequestTask task)
    {
        if (task.Method == "GET")
        {
            return UnityWebRequest.Get(task.Url);
        }

        if (task.Method == "POST_FORM" && task.FormFields != null)
        {
            return UnityWebRequest.Post(task.Url, task.FormFields);
        }

        // POST (raw body)
        var req = new UnityWebRequest(task.Url, "POST");
        if (!string.IsNullOrEmpty(task.Body))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(task.Body));
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", task.ContentType ?? "application/json");
        return req;
    }

    // ==================== 内部类型 ====================

    private class HttpRequestTask
    {
        public object Owner;
        public bool Cancelled;
        public UnityWebRequest ActiveRequest;
        public string Url;
        public string Method; // "GET" | "POST" | "POST_FORM"
        public string Body;
        public string ContentType;
        public Dictionary<string, string> FormFields;
        public float Timeout;

        private readonly List<Action<bool, string>> _callbacks =
            new List<Action<bool, string>>();

        public void AddCallback(Action<bool, string> cb)
        {
            if (cb != null) _callbacks.Add(cb);
        }

        public void Invoke(bool success, string result)
        {
            // 倒序遍历，回调内可能再次发请求修改列表
            for (int i = _callbacks.Count - 1; i >= 0; i--)
            {
                try
                {
                    _callbacks[i](success, result);
                }
                catch (Exception e)
                {
                    Debug.LogError("[HttpManager] callback exception: " + e);
                }
            }
        }
    }
}

/// <summary>
/// HttpManager 的协程驱动器，隐藏 GameObject，DontDestroyOnLoad。
/// </summary>
public class HttpManagerRunner : MonoBehaviour { }
