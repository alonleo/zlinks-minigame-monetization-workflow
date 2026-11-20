using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 侧边栏奖励弹窗 UI 脚本（platformTools 通用版，不依赖项目专属脚本）。
/// 负责弹窗内按钮（前往添加/领取/关闭）的点击和状态刷新。
/// desktopBtn 已移到 TTCommonUI，不在此处理。
///
/// 按钮逻辑：
///   无奖励 content 显示时（hasRewardContent=false）：
///     显示 enterSidebarBtn，隐藏 getRewardBtn
///     enterSidebarBtn 状态根据 SidebarState：GoToAdd 可点击，其余不可点击
///   有奖励 content-reward 显示时（hasRewardContent=true）：
///     GoToAdd  — 显示 enterSidebarBtn 可点击，隐藏 getRewardBtn
///     CanClaim — 隐藏 enterSidebarBtn，显示 getRewardBtn 可点击
///     Claimed  — 隐藏 enterSidebarBtn，显示 getRewardBtn 不可点击
///
/// bg 高度通过计算子节点高度 + padding 直接设置，不依赖 Content Size Fitter。
///
/// 用法：外部打开弹窗时调用 Show()，关闭时调用 Close() 或点关闭按钮。
/// </summary>
public class SidebarDlg : MonoBehaviour
{
    [SerializeField] private Button enterSidebarBtn;   // 前往添加按钮
    [SerializeField] private Button getRewardBtn;       // 领取按钮
    [SerializeField] private Button btnClose1;           // 关闭按钮
    [SerializeField] private Button btnClose2;           // 关闭按钮
    [SerializeField] private GameObject content;        // 无奖励内容
    [SerializeField] private GameObject contentReward;  // 有奖励内容
    [SerializeField] private RectTransform bgRect;      // 背板

    /// <summary>当前游戏是否有奖励可发，false 则显示无奖励内容</summary>
    [SerializeField] private bool hasRewardContent = true;

    [Header("Debug")]
    [SerializeField] private Button debugBtn;              // 调试按钮（点击10次切换输入框）
    [SerializeField] private InputField debugInputField; // 密码输入框

    private int _debugClickCount;
    private bool _debugInputVisible;

    private void Awake()
    {
        if (enterSidebarBtn != null)
            enterSidebarBtn.onClick.AddListener(OnEnterSidebarClick);
        if (getRewardBtn != null)
            getRewardBtn.onClick.AddListener(OnGetRewardClick);
        if (btnClose1 != null)
            btnClose1.onClick.AddListener(Close);
        if (btnClose2 != null)
            btnClose2.onClick.AddListener(Close);

        // 自动查找 debugBtn 和 debugInputField（不依赖 Inspector 赋值）
        if (debugBtn == null)
        {
            Transform btn = transform.Find("debugBtn");
            if (btn != null)
            {
                debugBtn = btn.GetComponent<Button>();
                if (debugBtn == null)
                    debugBtn = btn.gameObject.AddComponent<Button>();
            }
        }
        if (debugInputField == null)
        {
            Transform input = transform.Find("InputField");
            if (input != null)
                debugInputField = input.GetComponent<InputField>();
        }

        if (debugBtn != null)
        {
            debugBtn.onClick.AddListener(OnDebugBtnClick);
            Debug.Log(string.Format("[SidebarDlg] debugBtn 已绑定 name={0} active={1} interactable={2}",
                debugBtn.gameObject.name, debugBtn.gameObject.activeSelf, debugBtn.interactable));
        }
        else
        {
            Debug.LogWarning("[SidebarDlg] debugBtn 未找到，请确认 SidebarDlg 下有名为 debugBtn 的子节点");
        }
        if (debugInputField != null)
        {
            debugInputField.gameObject.SetActive(false);
            Debug.Log("[SidebarDlg] debugInputField 已绑定");
        }
    }

    private void OnEnable()
    {
        if (PlatformManager.HasInstance)
            PlatformManager.Instance.OnSidebarStateChanged += OnStateChanged;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (PlatformManager.HasInstance)
        {
            PlatformManager.Instance.OnSidebarStateChanged -= OnStateChanged;
            PlatformManager.Instance.ReportUiClosed("sidebar");
        }
    }

    /// <summary>外部调用打开弹窗</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        PlatformManager.Instance?.ReportUiShown("sidebar");
    }

    /// <summary>外部调用关闭弹窗</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>设置是否有奖励内容，外部调用后自动刷新</summary>
    public void SetRewardContent(bool hasReward)
    {
        hasRewardContent = hasReward;
        RefreshUI();
    }

    /// <summary>
    /// 根据侧边栏状态 + hasRewardContent 刷新 UI
    /// </summary>
    public void RefreshUI()
    {
        if (!PlatformManager.HasInstance) return;

        var state = PlatformManager.Instance.CurrentSidebarState;

        // 切换 content 显示（由 hasRewardContent 控制，与按钮状态无关）
        if (content != null) content.SetActive(!hasRewardContent);
        if (contentReward != null) contentReward.SetActive(hasRewardContent);

        // 按钮显隐 + interactable
        if (hasRewardContent)
        {
            if (enterSidebarBtn != null)
            {
                bool showEnter = state == PlatformBase.SidebarState.GoToAdd;
                enterSidebarBtn.gameObject.SetActive(showEnter);
                enterSidebarBtn.interactable = showEnter;
            }

            if (getRewardBtn != null)
            {
                bool showGet = state == PlatformBase.SidebarState.CanClaim
                               || state == PlatformBase.SidebarState.Claimed;
                getRewardBtn.gameObject.SetActive(showGet);
                getRewardBtn.interactable = (state == PlatformBase.SidebarState.CanClaim);
            }
        }
        else
        {
            if (enterSidebarBtn != null)
            {
                enterSidebarBtn.gameObject.SetActive(true);
                enterSidebarBtn.interactable = (state == PlatformBase.SidebarState.GoToAdd);
            }

            if (getRewardBtn != null)
            {
                getRewardBtn.gameObject.SetActive(false);
            }
        }

        // 直接计算设置 bg 高度
        UpdateBgHeight();
    }

    /// <summary>计算当前可见子节点高度，直接设置 bg 高度</summary>
    private void UpdateBgHeight()
    {
        if (bgRect == null) return;
        float baseHeight = hasRewardContent ? 882 : 660;
        bgRect.sizeDelta = new Vector2(bgRect.sizeDelta.x, baseHeight);
    }

    /// <summary>前往添加：跳转侧边栏</summary>
    private void OnEnterSidebarClick()
    {
        PlatformManager.Instance?.ReportEvent("sidebar_enter");
        PlatformManager.Instance?.goSideBar();
        Close();
    }

    /// <summary>领取奖励</summary>
    private void OnGetRewardClick()
    {
        PlatformManager.Instance?.ClaimSidebarReward();
        RefreshUI();
    }

    private void OnStateChanged(PlatformBase.SidebarState state)
    {
        RefreshUI();
    }

    private void OnDebugBtnClick()
    {
        _debugClickCount++;
        Debug.Log(string.Format("[SidebarDlg] debugBtn 点击 {0}/10", _debugClickCount));
        if (_debugClickCount >= 10)
        {
            _debugClickCount = 0;
            // 仅提示开发者配置状态，不允许玩家操作更改免广告开关
            Debug.Log("[MiniGameKit] 请由开发者在 AppConst 手动设置 isNoAd。");
            bool enabled = AppConst.isNoAd;
            Debug.Log(string.Format("[SidebarDlg] 免广告模式 {0}", enabled ? "已开启" : "已关闭"));
            PlatformManager.Instance?.ShowToast(enabled ? "免广告模式已开启" : "免广告模式已关闭");
        }
    }
}
