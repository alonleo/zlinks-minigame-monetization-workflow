#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if WEIXINMINIGAME && MINIGAME_KIT_WX_CONVERTER
using WeChatWASM;
#endif

namespace MiniGame.Editor
{
    // Native Tuanjie builds do not depend on the optional WeChatWASM converter package.
    public class DnSDKBuildHook : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
#if (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !MINIGAME_KIT_WX_CONVERTER
            string target = report.summary.platform.ToString();
            if (target.IndexOf("Weixin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(target, "MiniGame", StringComparison.OrdinalIgnoreCase))
                ProcessOutput(report.summary.outputPath);
            else if (target == "WebGL" && AppConst.DnSDKConfigured)
                throw new BuildFailedException("dnSDK: WebGL conversion requires the official WeChat converter and MINIGAME_KIT_WX_CONVERTER; native Tuanjie builds use the Weixin/MiniGame target.");
#endif
        }

        public static void ProcessOutput(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new BuildFailedException("dnSDK: missing WeChat build output directory.");
            string root = Path.GetFullPath(outputPath);
            string game = null;
            // Exact supported layouts only; never recursively patch an unrelated game.js.
            foreach (string relative in new[] { "game.js", "minigame/game.js", "wechat/minigame/game.js" })
            {
                string candidate = Path.Combine(root, relative);
                if (File.Exists(candidate)) { game = candidate; break; }
            }
            if (game == null)
            {
                if (AppConst.DnSDKConfigured)
                    throw new BuildFailedException("dnSDK: game.js was not found in the WeChat output. Verify the export layout or invoke ProcessOutput after conversion.");
                return; // dnSDK is disabled and there is no generated script to clean up.
            }
            string dst = Path.GetDirectoryName(game);
            string original = File.ReadAllText(game);
            string content = Regex.Replace(original,
                @"(?m)^[ \t]*import[ \t]+['""]\./dn-sdk-(?:config|init)(?:\.js)?['""][ \t]*;?[ \t]*(?:\r?\n|$)", "");
            string configPath = Path.Combine(dst, "dn-sdk-config.js");
            if (!AppConst.DnSDKConfigured)
            {
                if (original != content) File.WriteAllText(game, content, new UTF8Encoding(false));
                if (original != content || File.Exists(configPath))
                    File.WriteAllText(configPath, "GameGlobal.dnSDKConfig = {enabled:false};\n", new UTF8Encoding(false));
                return;
            }
            if (!AppConst.HasValidDnCredentials)
                throw new BuildFailedException("Invalid dnSDK credentials; values omitted");
            string src = Path.Combine(UnityEngine.Application.dataPath, "MiniGameKit/SDK/WeChatTemplate");
            string[] files = { "dn-sdk-init.js", "dn-sdk/index.js" };
            foreach (string file in files)
                if (!File.Exists(Path.Combine(src, file)))
                    throw new BuildFailedException("Missing dnSDK source: " + file);
            foreach (string file in files)
            {
                string target = Path.Combine(dst, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(src, file), target, true);
            }
            var config = new {
                enabled = true, debug = AppConst.TelemetryDebugEnabled,
                user_action_set_id = long.Parse(AppConst.EffectiveDnUserActionSetId),
                secret_key = AppConst.EffectiveDnSecretKey, appid = AppConst.AppId
            };
            File.WriteAllText(configPath, "GameGlobal.dnSDKConfig = " + JsonConvert.SerializeObject(config) + ";\n", new UTF8Encoding(false));
            File.WriteAllText(game, "import './dn-sdk-config.js';\nimport './dn-sdk-init.js';\n" + content, new UTF8Encoding(false));
        }
    }

#if WEIXINMINIGAME && MINIGAME_KIT_WX_CONVERTER
    // Required for WXConvertCore.DoExport on both Tuanjie WeixinMiniGame and Unity WebGL.
    // Its inner BuildPlayer callback precedes template generation.
    public class DnSDKConverterBuildHook : LifeCycleBase
    {
        public override void beforeExport()
        {
            // The converter invokes BuildPlayer before creating game.js. Validate configuration
            // here; inject only after its generated template exists.
            if (AppConst.DnSDKConfigured && !AppConst.HasValidDnCredentials)
                throw new BuildFailedException("Invalid dnSDK credentials; values omitted");
        }

        public override void afterBuildTemplate()
        {
            var conf = UnityUtil.GetEditorConf();
            if (conf == null || string.IsNullOrEmpty(conf.ProjectConf.DST))
                throw new BuildFailedException("Missing WeChat converter output directory");
            DnSDKBuildHook.ProcessOutput(conf.ProjectConf.DST);
        }
    }
#endif
}
#endif
