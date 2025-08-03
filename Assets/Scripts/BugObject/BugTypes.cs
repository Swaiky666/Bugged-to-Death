// BugTypes.cs - 重构后的数据结构，使用魔法值系统替代积分
using System;
using UnityEngine;

namespace BugFixerGame
{
    // 游戏状态枚举
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }

    // Bug类型枚举（保持不变）
    public enum BugType
    {
        None,
        ObjectFlickering,
        CollisionMissing,
        WrongOrMissingMaterial,
        WrongObject,
        MissingObject,
        ObjectShaking,
        ObjectMovedOrClipping
    }

    // 更新的游戏数据，使用魔法值系统替代积分
    [System.Serializable]
    public class GameData
    {
        [Header("游戏状态")]
        public GameState stateBeforePause;

        [Header("魔法值系统")]
        public int currentMana;
        public int maxMana;

        [Header("游戏统计")]
        public int bugsFixed;           // 修复的Bug数量
        public int wrongDetections;     // 错误检测次数
        public int gamesPlayed;         // 游戏局数
        public float totalPlayTime;     // 总游戏时间

        [Header("最佳记录")]
        public int bestBugsFixed;       // 单局最多修复Bug数
        public int longestStreak;       // 最长连续正确检测
        public float fastestBugFix;     // 最快Bug修复时间

        // 默认构造函数
        public GameData()
        {
            stateBeforePause = GameState.MainMenu;
            currentMana = 5;
            maxMana = 5;
            bugsFixed = 0;
            wrongDetections = 0;
            gamesPlayed = 0;
            totalPlayTime = 0f;
            bestBugsFixed = 0;
            longestStreak = 0;
            fastestBugFix = 0f;
        }

        // 重置当前游戏数据（保留历史记录）
        public void ResetGameSession()
        {
            currentMana = maxMana;
            bugsFixed = 0;
            wrongDetections = 0;
        }

        // 更新最佳记录
        public void UpdateBestRecords(int sessionBugsFixed, int sessionStreak, float sessionFastestFix)
        {
            if (sessionBugsFixed > bestBugsFixed)
                bestBugsFixed = sessionBugsFixed;

            if (sessionStreak > longestStreak)
                longestStreak = sessionStreak;

            if (fastestBugFix == 0f || (sessionFastestFix > 0f && sessionFastestFix < fastestBugFix))
                fastestBugFix = sessionFastestFix;
        }

        // 计算成功率
        public float GetSuccessRate()
        {
            int totalDetections = bugsFixed + wrongDetections;
            return totalDetections > 0 ? (float)bugsFixed / totalDetections : 0f;
        }

        // 获取魔法值百分比
        public float GetManaPercentage()
        {
            return maxMana > 0 ? (float)currentMana / maxMana : 0f;
        }

        // 获取游戏统计描述
        public string GetStatsDescription()
        {
            return $"修复Bug: {bugsFixed}, 错误检测: {wrongDetections}, 成功率: {GetSuccessRate():P1}";
        }
    }

    // 魔法值相关事件参数
    [System.Serializable]
    public class ManaEventArgs : EventArgs
    {
        public int currentMana;
        public int maxMana;
        public int change;              // 变化量（正数为恢复，负数为消耗）
        public string reason;           // 变化原因

        public ManaEventArgs(int current, int max, int delta, string changeReason = "")
        {
            currentMana = current;
            maxMana = max;
            change = delta;
            reason = changeReason;
        }

        public float GetManaPercentage()
        {
            return maxMana > 0 ? (float)currentMana / maxMana : 0f;
        }

        public bool IsEmpty()
        {
            return currentMana <= 0;
        }

        public bool IsFull()
        {
            return currentMana >= maxMana;
        }
    }

    // Bug检测结果枚举
    public enum DetectionResult
    {
        Success,        // 成功检测到真Bug
        Failure,        // 检测到非Bug物体
        AlreadyFixed,   // Bug已经被修复
        NoMana          // 没有魔法值
    }

    // Bug修复结果
    [System.Serializable]
    public class BugFixResult
    {
        public BugObject bugObject;
        public BugType bugType;
        public DetectionResult result;
        public float fixTime;           // 修复用时
        public int manaConsumed;        // 消耗的魔法值
        public string objectName;       // 物体名称

        public BugFixResult(BugObject bug, DetectionResult detectionResult, float time = 0f, int manaCost = 0)
        {
            bugObject = bug;
            bugType = bug != null ? bug.GetBugType() : BugType.None;
            result = detectionResult;
            fixTime = time;
            manaConsumed = manaCost;
            objectName = bug != null ? bug.name : "未知物体";
        }

        public BugFixResult(GameObject obj, DetectionResult detectionResult, float time = 0f, int manaCost = 0)
        {
            bugObject = obj?.GetComponent<BugObject>();
            bugType = bugObject != null ? bugObject.GetBugType() : BugType.None;
            result = detectionResult;
            fixTime = time;
            manaConsumed = manaCost;
            objectName = obj != null ? obj.name : "未知物体";
        }

        public bool IsSuccess()
        {
            return result == DetectionResult.Success;
        }

        public string GetResultDescription()
        {
            switch (result)
            {
                case DetectionResult.Success:
                    return $"成功修复 {GetBugTypeDisplayName(bugType)}";
                case DetectionResult.Failure:
                    return $"错误检测 {objectName}";
                case DetectionResult.AlreadyFixed:
                    return $"Bug已修复 {objectName}";
                case DetectionResult.NoMana:
                    return "魔法值不足";
                default:
                    return "未知结果";
            }
        }

        private string GetBugTypeDisplayName(BugType type)
        {
            switch (type)
            {
                case BugType.ObjectFlickering: return "物体闪烁";
                case BugType.CollisionMissing: return "碰撞缺失";
                case BugType.WrongOrMissingMaterial: return "错误材质";
                case BugType.WrongObject: return "错误物体";
                case BugType.MissingObject: return "缺失物体";
                case BugType.ObjectShaking: return "物体震动";
                case BugType.ObjectMovedOrClipping: return "位移穿模";
                default: return type.ToString();
            }
        }
    }

    // 游戏设置数据
    [System.Serializable]
    public class GameSettings
    {
        [Header("魔法值设置")]
        [Range(1, 10)]
        public int maxMana = 5;
        [Range(1, 5)]
        public int failurePenalty = 1;
        public bool enableManaRegeneration = false;
        public float manaRegenRate = 1f;        // 每秒恢复的魔法值

        [Header("检测设置")]
        [Range(0.5f, 5f)]
        public float holdTime = 2f;
        public bool showProgressUI = true;

        [Header("魔法球UI设置")]
        public float orbSpacing = 60f;
        public bool useHorizontalLayout = true;
        public Vector3 startPosition = Vector3.zero;

        [Header("视觉设置")]
        public Color fullManaColor = new Color(0.2f, 0.6f, 1f, 1f);     // 蓝色
        public Color lowManaColor = new Color(1f, 0.8f, 0f, 1f);        // 黄色
        public Color emptyManaColor = new Color(0.5f, 0.5f, 0.5f, 1f);  // 灰色
        public bool enablePulseAnimation = true;

        [Header("难度设置")]
        public float bugSpawnRate = 1f;         // Bug生成频率倍数
        public bool enableTimeLimit = false;
        public float timeLimit = 300f;          // 时间限制（秒）

        // 应用设置到游戏管理器
        public void ApplyToGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetMaxMana(maxMana);
                GameManager.Instance.SetFailurePenalty(failurePenalty);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetOrbSpacing(orbSpacing);
                UIManager.Instance.SetHorizontalLayout(useHorizontalLayout);
            }

            Debug.Log("游戏设置已应用");
        }

        // 验证设置的有效性
        public bool ValidateSettings()
        {
            if (maxMana < 1 || maxMana > 10)
            {
                Debug.LogWarning("最大魔法值应该在1-10之间");
                return false;
            }

            if (failurePenalty < 1 || failurePenalty > maxMana)
            {
                Debug.LogWarning("失败惩罚应该在1到最大魔法值之间");
                return false;
            }

            if (holdTime < 0.1f)
            {
                Debug.LogWarning("长按时间不能小于0.1秒");
                return false;
            }

            return true;
        }

        // 重置为默认设置
        public void ResetToDefaults()
        {
            maxMana = 5;
            failurePenalty = 1;
            enableManaRegeneration = false;
            manaRegenRate = 1f;
            holdTime = 2f;
            showProgressUI = true;
            orbSpacing = 60f;
            useHorizontalLayout = true;
            startPosition = Vector3.zero;
            fullManaColor = new Color(0.2f, 0.6f, 1f, 1f);
            lowManaColor = new Color(1f, 0.8f, 0f, 1f);
            emptyManaColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            enablePulseAnimation = true;
            bugSpawnRate = 1f;
            enableTimeLimit = false;
            timeLimit = 300f;
        }
    }

    // 游戏统计助手类
    public static class GameStatsHelper
    {
        public static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        public static string FormatPercentage(float value)
        {
            return $"{(value * 100f):F1}%";
        }

        public static Color GetManaColor(int currentMana, int maxMana, GameSettings settings)
        {
            if (maxMana <= 0) return settings.emptyManaColor;

            float percentage = (float)currentMana / maxMana;

            if (percentage <= 0f)
                return settings.emptyManaColor;
            else if (percentage <= 0.33f)
                return Color.Lerp(settings.emptyManaColor, settings.lowManaColor, percentage * 3f);
            else if (percentage <= 0.66f)
                return Color.Lerp(settings.lowManaColor, settings.fullManaColor, (percentage - 0.33f) * 3f);
            else
                return settings.fullManaColor;
        }

        public static string GetBugTypeDisplayName(BugType bugType)
        {
            switch (bugType)
            {
                case BugType.ObjectFlickering: return "物体闪烁";
                case BugType.CollisionMissing: return "碰撞缺失";
                case BugType.WrongOrMissingMaterial: return "错误材质";
                case BugType.WrongObject: return "错误物体";
                case BugType.MissingObject: return "缺失物体";
                case BugType.ObjectShaking: return "物体震动";
                case BugType.ObjectMovedOrClipping: return "位移穿模";
                default: return bugType.ToString();
            }
        }
    }
}