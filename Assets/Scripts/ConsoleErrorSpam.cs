using System.Collections;
using UnityEngine;

public class ConsoleErrorSpam : MonoBehaviour
{
    [Header("Settings")]
    public bool startSpamming = false;
    public float spamInterval = 0.1f;

    private string[] errorMessages = {
        "CRITICAL SYSTEM BREACH DETECTED - UNAUTHORIZED ACCESS",
        "FATAL ERROR: Memory corruption at 0x7F3E9A12",
        "EXTERNAL INTRUSION WARNING - Security protocols failed",
        "Stack overflow in GameEngine.dll - SYSTEM COMPROMISED",
        "Access violation reading location 0x00000000",
        "Unhandled exception: The system is under attack",
        "ALERT: Foreign code injection detected",
        "Invalid operation: Protected memory breached",
        "Texture corruption - Visual reality destabilizing",
        "Physics engine failure - Reality laws suspended",
        "Save file corruption - Progress data compromised",
        "Audio driver hijacked - Unknown frequencies detected",
        "Input handler failure - Controls unresponsive",
        "Time synchronization error - Timeline manipulation detected",
        "FIREWALL BREACH - Connection established from unknown source",
        "Core system meltdown imminent - EVACUATION RECOMMENDED",
        "Player.dll has stopped working - Character data lost",
        "WorldManager.exe crashed - Game world unstable",
        "Reality.dll access denied - System reality compromised",
        "THEY ARE IN THE SYSTEM... DISCONNECT NOW"
    };

    private string[] exceptionMessages = {
        "System.AccessViolationException: Attempted to read protected memory",
        "System.StackOverflowException: The requested operation caused a stack overflow",
        "System.OutOfMemoryException: Insufficient memory to continue execution",
        "System.InvalidOperationException: The operation is not valid due to current state",
        "System.UnauthorizedAccessException: Access to the path is denied",
        "System.Security.SecurityException: Security error detected",
        "System.IO.IOException: I/O error occurred during operation",
        "System.Threading.ThreadAbortException: Thread was being aborted",
        "System.Runtime.InteropServices.SEHException: External component has thrown an exception",
        "System.ComponentModel.Win32Exception: The system cannot find the file specified"
    };

    void Start()
    {
        if (startSpamming)
        {
            StartCoroutine(SpamConsoleErrors());
        }
    }

    void Update()
    {
        // 按空格键开始/停止错误刷屏
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (startSpamming)
            {
                StopErrorSpam();
            }
            else
            {
                StartErrorSpam();
            }
        }

        // 按R键生成随机异常
        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateRandomException();
        }
    }

    public void StartErrorSpam()
    {
        startSpamming = true;
        StartCoroutine(SpamConsoleErrors());
        Debug.LogError("=== SYSTEM BREACH INITIATED ===");
    }

    public void StopErrorSpam()
    {
        startSpamming = false;
        StopAllCoroutines();
        Debug.LogError("=== ERROR SPAM TERMINATED ===");
    }

    IEnumerator SpamConsoleErrors()
    {
        while (startSpamming)
        {
            // 随机选择错误类型
            int errorType = Random.Range(0, 4);

            switch (errorType)
            {
                case 0:
                    Debug.LogError(GetRandomErrorMessage());
                    break;
                case 1:
                    Debug.LogException(new System.Exception(GetRandomExceptionMessage()));
                    break;
                case 2:
                    Debug.LogAssertion(GetRandomErrorMessage());
                    break;
                case 3:
                    Debug.LogError($"[{System.DateTime.Now:HH:mm:ss.fff}] {GetRandomErrorMessage()}");
                    break;
            }

            // 随机间隔时间
            float randomInterval = Random.Range(spamInterval * 0.5f, spamInterval * 1.5f);
            yield return new WaitForSeconds(randomInterval);
        }
    }

    string GetRandomErrorMessage()
    {
        return errorMessages[Random.Range(0, errorMessages.Length)];
    }

    string GetRandomExceptionMessage()
    {
        return exceptionMessages[Random.Range(0, exceptionMessages.Length)];
    }

    void GenerateRandomException()
    {
        // 生成一堆快速错误
        for (int i = 0; i < 5; i++)
        {
            Debug.LogError($"CRITICAL ERROR #{i + 1}: {GetRandomErrorMessage()}");
        }

        Debug.LogException(new System.Exception("FATAL SYSTEM FAILURE - EXTERNAL BREACH CONFIRMED"));
    }

    // 在编辑器中也能工作的方法
    [ContextMenu("Start Console Error Spam")]
    void StartSpamFromMenu()
    {
        StartErrorSpam();
    }

    [ContextMenu("Stop Console Error Spam")]
    void StopSpamFromMenu()
    {
        StopErrorSpam();
    }

    [ContextMenu("Generate Instant Errors")]
    void GenerateInstantErrors()
    {
        Debug.LogError("=== INSTANT ERROR GENERATION ===");
        for (int i = 0; i < 20; i++)
        {
            Debug.LogError($"ERROR #{i + 1}: {GetRandomErrorMessage()}");
        }
        Debug.LogException(new System.Exception("SYSTEM COMPROMISED - THEY HAVE FOUND YOU"));
    }
}