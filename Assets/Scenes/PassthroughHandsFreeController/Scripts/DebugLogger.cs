using UnityEngine;
using System.Runtime.CompilerServices; // Necessário para CallerMemberName

public static class DebugLogger
{
    // Tag format
    private static string FormatTag(string className, string methodName)
    {
        return $"[PFHFC - {className}::{methodName}]";
    }

    // [CallerMemberName] gets the name of the method that called the function
    // [CallerFilePath] gets the file path of from witch the function were called
    private static string GetClassNameFromPath(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public static void Log(object message, 
                             [CallerMemberName] string methodName = "", 
                             [CallerFilePath] string filePath = "")
    {
        string className = GetClassNameFromPath(filePath);
        Debug.Log($"<b>{FormatTag(className, methodName)}  :</b>  {message}");
    }

    public static void Log(object message, Object context, 
                             [CallerMemberName] string methodName = "", 
                             [CallerFilePath] string filePath = "")
    {
        string className = GetClassNameFromPath(filePath);
        Debug.Log($"<b>{FormatTag(className, methodName)}  :</b>  {message}", context);
    }

    public static void LogWarning(object message, 
                               [CallerMemberName] string methodName = "", 
                               [CallerFilePath] string filePath = "")
    {
        string className = GetClassNameFromPath(filePath);
        Debug.LogWarning($"<b><color=yellow>{FormatTag(className, methodName)}</color>  :</b>  {message}");
    }

    public static void LogError(object message, 
                             [CallerMemberName] string methodName = "", 
                             [CallerFilePath] string filePath = "")
    {
        string className = GetClassNameFromPath(filePath);
        Debug.LogError($"<b><color=red>{FormatTag(className, methodName)}</color>  :</b>  {message}");
    }
}
