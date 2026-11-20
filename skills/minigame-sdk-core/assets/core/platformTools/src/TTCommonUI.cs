using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抖音通用功能 UI：挂在主界面常驻按钮组上。
/// 不依赖项目专属脚本，所有平台调用统一走 PlatformManager 分发。
/// 整个 platformTools 目录可直接复制到下个项目。
/// </summary>
public class TTCommonUI : MonoBehaviour
{
    [Header("按钮引用（可为空，空则隐藏）")]
    [SerializeField] private Button sideBtn;        // 前往添加（打开侧边栏弹窗）
    [SerializeField] private Button desktopBtn;    // 添加桌面快捷方式
    [SerializeField] private Button shareBtn;      // 分享
    [SerializeField] private Button rankBtn;       // 排行榜
    [SerializeField] private Button moregameBtn;   // 更多游戏


    private SidebarDlg _sidebarDlg;

    private void Awake()
    {
        Bind(sideBtn, OnSideBtnClick);
        Bind(desktopBtn, OnDesktopBtnClick);
        Bind(shareBtn, OnShareBtnClick);
        Bind(rankBtn, OnRankBtnClick);
        Bind(moregameBtn, OnMoreGameBtnClick);
    }

    private void Start()
    {
        bool isDouyin = PlatformManager.Instance?.GetPlatformName() == "douyin";
        if (sideBtn != null) sideBtn.gameObject.SetActive(isDouyin);
        else if (isDouyin) Debug.LogError("[TTCommonUI] 抖音首页缺少侧边栏入口引用");
        if (desktopBtn != null) desktopBtn.gameObject.SetActive(isDouyin);

        ShowSidebarDlg(false);
    }

    // ==================== 按钮点击逻辑（统一走 PlatformManager） ====================

    /// <summary>点击 sideBtn：打开侧边栏奖励弹窗</summary>
    private void OnSideBtnClick()
    {
        Debug.Log("[TTCommonUI] 打开侧边栏弹窗");
        PlatformManager.Instance?.ReportEvent("sidebar_click");
        ShowSidebarDlg();
    }

    /// <summary>点击 desktopBtn：添加桌面快捷方式</summary>
    private void OnDesktopBtnClick()
    {
        Debug.Log("[TTCommonUI] 添加桌面快捷方式");
        PlatformManager.Instance?.ReportEvent("desktop_add_click");
        PlatformManager.Instance?.AddShortcut(success =>
        {
            Debug.Log("[TTCommonUI] AddShortcut 结果: " + success);
            PlatformManager.Instance?.ReportEvent(success ? "desktop_add_success" : "desktop_add_fail");
        });
    }

    /// <summary>点击 shareBtn：分享</summary>
    private void OnShareBtnClick()
    {
        Debug.Log("[TTCommonUI] 分享");
        PlatformManager.Instance?.Share("sidebar", result =>
        {
            Debug.Log("[TTCommonUI] 分享结果: " + result);
        });
    }

    /// <summary>点击 rankBtn：拉起好友排行榜（拉起前自动上报最新分数）</summary>
    private void OnRankBtnClick()
    {
        Debug.Log("[TTCommonUI] 拉起排行榜");
        PlatformManager.Instance?.ShowRankList();
    }

    /// <summary>点击 moregameBtn：更多游戏</summary>
    private void OnMoreGameBtnClick()
    {
        Debug.Log("[TTCommonUI] 更多游戏（待接入）");
    }

    // ==================== 侧边栏弹窗 ====================

    /// <summary>获取或创建侧边栏弹窗实例并显示</summary>
    private void ShowSidebarDlg(bool active = true)
    {
        if (_sidebarDlg == null)
        {
            _sidebarDlg = FindObjectOfType<SidebarDlg>(true);
        }
        if (_sidebarDlg == null)
        {
            if (active) Debug.LogWarning("[TTCommonUI] 侧边栏弹窗未绑定");
            return;
        }
        if (active)
        {
            _sidebarDlg.Show();
        }
        else{
            _sidebarDlg.Close();
        }
    }



    // ==================== 工具方法 ====================

    private void Bind(Button btn, Action action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => action());
    }

}
