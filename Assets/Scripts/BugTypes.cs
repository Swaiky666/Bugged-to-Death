// �ع���� BugTypes.cs����������Ҫ��ö�٣�
using System;
using UnityEngine;

namespace BugFixerGame
{
    // �򻯺����Ϸ״̬��ֻ�������˵�����ͣ��
    public enum GameState
    {
        MainMenu,
        Paused
    }

    // ���·����Bug����ö��
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

    // ��Ϸ������
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;
        public int highScore;
        public int gamesPlayed;
    }
}
