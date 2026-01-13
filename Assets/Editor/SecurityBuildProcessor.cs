using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Core.Editor
{
    public class SecurityBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Automatically allow HTTP for development builds
            // This prevents "Non-secure network connections disabled" errors in local dev
            if ((report.summary.options & BuildOptions.Development) != 0)
            {
                Debug.Log("[SecurityBuildProcessor] Development Build detected: Setting InsecureHttpOption to DevelopmentOnly.");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            }
            else
            {
                // For release builds, we might want to enforce HTTPS, but let's stick to NotAllowed or whatever was set, 
                // OR enforce DevelopmentOnly which handles both automatically.
                // DevelopmentOnly = Allowed in Dev, Not Allowed in Release.
                Debug.Log("[SecurityBuildProcessor] Setting InsecureHttpOption to DevelopmentOnly.");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            }
        }
    }
}
