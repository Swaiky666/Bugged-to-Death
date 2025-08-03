// BugTypes.cs - �ع�������ݽṹ��ʹ��ħ��ֵϵͳ�������
using System;
using UnityEngine;

namespace BugFixerGame
{
    // ��Ϸ״̬ö��
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }

    // Bug����ö�٣����ֲ��䣩
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

    // ���µ���Ϸ���ݣ�ʹ��ħ��ֵϵͳ�������
    [System.Serializable]
    public class GameData
    {
        [Header("��Ϸ״̬")]
        public GameState stateBeforePause;

        [Header("ħ��ֵϵͳ")]
        public int currentMana;
        public int maxMana;

        [Header("��Ϸͳ��")]
        public int bugsFixed;           // �޸���Bug����
        public int wrongDetections;     // ���������
        public int gamesPlayed;         // ��Ϸ����
        public float totalPlayTime;     // ����Ϸʱ��

        [Header("��Ѽ�¼")]
        public int bestBugsFixed;       // ��������޸�Bug��
        public int longestStreak;       // �������ȷ���
        public float fastestBugFix;     // ���Bug�޸�ʱ��

        // Ĭ�Ϲ��캯��
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

        // ���õ�ǰ��Ϸ���ݣ�������ʷ��¼��
        public void ResetGameSession()
        {
            currentMana = maxMana;
            bugsFixed = 0;
            wrongDetections = 0;
        }

        // ������Ѽ�¼
        public void UpdateBestRecords(int sessionBugsFixed, int sessionStreak, float sessionFastestFix)
        {
            if (sessionBugsFixed > bestBugsFixed)
                bestBugsFixed = sessionBugsFixed;

            if (sessionStreak > longestStreak)
                longestStreak = sessionStreak;

            if (fastestBugFix == 0f || (sessionFastestFix > 0f && sessionFastestFix < fastestBugFix))
                fastestBugFix = sessionFastestFix;
        }

        // ����ɹ���
        public float GetSuccessRate()
        {
            int totalDetections = bugsFixed + wrongDetections;
            return totalDetections > 0 ? (float)bugsFixed / totalDetections : 0f;
        }

        // ��ȡħ��ֵ�ٷֱ�
        public float GetManaPercentage()
        {
            return maxMana > 0 ? (float)currentMana / maxMana : 0f;
        }

        // ��ȡ��Ϸͳ������
        public string GetStatsDescription()
        {
            return $"�޸�Bug: {bugsFixed}, ������: {wrongDetections}, �ɹ���: {GetSuccessRate():P1}";
        }
    }

    // ħ��ֵ����¼�����
    [System.Serializable]
    public class ManaEventArgs : EventArgs
    {
        public int currentMana;
        public int maxMana;
        public int change;              // �仯��������Ϊ�ָ�������Ϊ���ģ�
        public string reason;           // �仯ԭ��

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

    // Bug�����ö��
    public enum DetectionResult
    {
        Success,        // �ɹ���⵽��Bug
        Failure,        // ��⵽��Bug����
        AlreadyFixed,   // Bug�Ѿ����޸�
        NoMana          // û��ħ��ֵ
    }

    // Bug�޸����
    [System.Serializable]
    public class BugFixResult
    {
        public BugObject bugObject;
        public BugType bugType;
        public DetectionResult result;
        public float fixTime;           // �޸���ʱ
        public int manaConsumed;        // ���ĵ�ħ��ֵ
        public string objectName;       // ��������

        public BugFixResult(BugObject bug, DetectionResult detectionResult, float time = 0f, int manaCost = 0)
        {
            bugObject = bug;
            bugType = bug != null ? bug.GetBugType() : BugType.None;
            result = detectionResult;
            fixTime = time;
            manaConsumed = manaCost;
            objectName = bug != null ? bug.name : "δ֪����";
        }

        public BugFixResult(GameObject obj, DetectionResult detectionResult, float time = 0f, int manaCost = 0)
        {
            bugObject = obj?.GetComponent<BugObject>();
            bugType = bugObject != null ? bugObject.GetBugType() : BugType.None;
            result = detectionResult;
            fixTime = time;
            manaConsumed = manaCost;
            objectName = obj != null ? obj.name : "δ֪����";
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
                    return $"�ɹ��޸� {GetBugTypeDisplayName(bugType)}";
                case DetectionResult.Failure:
                    return $"������ {objectName}";
                case DetectionResult.AlreadyFixed:
                    return $"Bug���޸� {objectName}";
                case DetectionResult.NoMana:
                    return "ħ��ֵ����";
                default:
                    return "δ֪���";
            }
        }

        private string GetBugTypeDisplayName(BugType type)
        {
            switch (type)
            {
                case BugType.ObjectFlickering: return "������˸";
                case BugType.CollisionMissing: return "��ײȱʧ";
                case BugType.WrongOrMissingMaterial: return "�������";
                case BugType.WrongObject: return "��������";
                case BugType.MissingObject: return "ȱʧ����";
                case BugType.ObjectShaking: return "������";
                case BugType.ObjectMovedOrClipping: return "λ�ƴ�ģ";
                default: return type.ToString();
            }
        }
    }

    // ��Ϸ��������
    [System.Serializable]
    public class GameSettings
    {
        [Header("ħ��ֵ����")]
        [Range(1, 10)]
        public int maxMana = 5;
        [Range(1, 5)]
        public int failurePenalty = 1;
        public bool enableManaRegeneration = false;
        public float manaRegenRate = 1f;        // ÿ��ָ���ħ��ֵ

        [Header("�������")]
        [Range(0.5f, 5f)]
        public float holdTime = 2f;
        public bool showProgressUI = true;

        [Header("ħ����UI����")]
        public float orbSpacing = 60f;
        public bool useHorizontalLayout = true;
        public Vector3 startPosition = Vector3.zero;

        [Header("�Ӿ�����")]
        public Color fullManaColor = new Color(0.2f, 0.6f, 1f, 1f);     // ��ɫ
        public Color lowManaColor = new Color(1f, 0.8f, 0f, 1f);        // ��ɫ
        public Color emptyManaColor = new Color(0.5f, 0.5f, 0.5f, 1f);  // ��ɫ
        public bool enablePulseAnimation = true;

        [Header("�Ѷ�����")]
        public float bugSpawnRate = 1f;         // Bug����Ƶ�ʱ���
        public bool enableTimeLimit = false;
        public float timeLimit = 300f;          // ʱ�����ƣ��룩

        // Ӧ�����õ���Ϸ������
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

            Debug.Log("��Ϸ������Ӧ��");
        }

        // ��֤���õ���Ч��
        public bool ValidateSettings()
        {
            if (maxMana < 1 || maxMana > 10)
            {
                Debug.LogWarning("���ħ��ֵӦ����1-10֮��");
                return false;
            }

            if (failurePenalty < 1 || failurePenalty > maxMana)
            {
                Debug.LogWarning("ʧ�ܳͷ�Ӧ����1�����ħ��ֵ֮��");
                return false;
            }

            if (holdTime < 0.1f)
            {
                Debug.LogWarning("����ʱ�䲻��С��0.1��");
                return false;
            }

            return true;
        }

        // ����ΪĬ������
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

    // ��Ϸͳ��������
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
                case BugType.ObjectFlickering: return "������˸";
                case BugType.CollisionMissing: return "��ײȱʧ";
                case BugType.WrongOrMissingMaterial: return "�������";
                case BugType.WrongObject: return "��������";
                case BugType.MissingObject: return "ȱʧ����";
                case BugType.ObjectShaking: return "������";
                case BugType.ObjectMovedOrClipping: return "λ�ƴ�ģ";
                default: return bugType.ToString();
            }
        }
    }
}