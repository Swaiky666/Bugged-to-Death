using System;
using UnityEngine;

namespace BugFixerGame
{
    // ��Ϸ״̬ö��
    public enum GameState
    {
        MainMenu,           // ���˵�
        GameStart,          // ��Ϸ��ʼ����
        MemoryPhase,        // ����׶Σ��۲���ȷ���䣩
        TransitionToCheck,  // ���ɵ����׶�
        CheckPhase,         // ���׶Σ�Ѱ��bug��
        RoomResult,         // ��������������
        GameEnd,            // ��Ϸ����
        Paused              // ��ͣ
    }

    // ���·����Bug����ö��
    public enum BugType
    {
        None,
        ObjectFlickering,           // ������˸
        CollisionMissing,           // ��ײȱʧ
        WrongOrMissingMaterial,     // �����ȱʧ����
        WrongObject,                // �������壨��ʾ����ȷ�����壩
        MissingObject,              // ȱʧ���壨������ʧ/͸����
        ObjectShaking,              // ������
        ObjectMovedOrClipping       // ����λ�ƻ�ģ
    }

    // ������������
    [System.Serializable]
    public class RoomConfig
    {
        public int roomId;
        public GameObject normalRoomPrefab;     // ��������Ԥ��
        public GameObject buggyRoomPrefab;      // ��Bug�ķ���Ԥ�ƣ���ѡ��
        public bool hasBug;                     // ��������Ƿ�涨��Bug
        public BugType bugType;                 // �涨��Bug����

        [UnityEngine.TextArea(2, 4)]
        public string bugDescription;           // Bug���������ڵ��ԣ�
    }

    // ����������
    public enum RoomResult
    {
        Perfect,        // ��������ȷʶ��bug״̬��
        Wrong,          // �������л�©�У�
        Timeout         // ��ʱ�������ʱ�����ƣ�
    }

    // ��Ϸ�������
    public enum GameEnding
    {
        Perfect,        // 7-10�� �������
        Good,           // 6�� ���ý��  
        Bad             // 0-5�� �����
    }

    // ��Ϸ������
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;

        // ������Ӹ�����Ҫ�������Ϸ����
        public int highScore;
        public int gamesPlayed;
    }
}