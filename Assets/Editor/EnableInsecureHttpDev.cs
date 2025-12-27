#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Ensures HTTP (non-HTTPS) requests are allowed during development.
// Equivalent to Player Settings → Other Settings → Allow downloads over HTTP.
[InitializeOnLoad]
public static class EnableInsecureHttpDev
{
    static EnableInsecureHttpDev()
    {
        Debug.Log("[EnableInsecureHttpDev] Initializing...");
        
        try
        {
            // Unity 2020+ API: Allow insecure HTTP for development
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            Debug.Log("[✓ EnableInsecureHttpDev] HTTP allowed for localhost connections");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[EnableInsecureHttpDev] Warning: " + ex.Message);
        }
    }
}
#endif
