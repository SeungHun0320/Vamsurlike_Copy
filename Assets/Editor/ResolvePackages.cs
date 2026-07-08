using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public static class ResolvePackages
{
    public static void Execute()
    {
        Debug.Log("[Build] Requesting Package Manager Client.Resolve()...");
        Client.Resolve();
    }
}
