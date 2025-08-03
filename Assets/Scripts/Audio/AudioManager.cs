// AudioManager.cs - 超级简化的音频管理器
using UnityEngine;

namespace BugFixerGame
{
    public class AudioManager : MonoBehaviour
    {
        [Header("背景音乐")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip gameMusic;
        [SerializeField] private AudioClip ambientSound;

        [Header("音效")]
        [SerializeField] private AudioClip castingSound;
        [SerializeField] private AudioClip bugFixSuccessSound;
        [SerializeField] private AudioClip bugFixFailSound;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip gameStartSound;

        [Header("音量设置")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        // 简单的AudioSource组件
        private AudioSource musicSource;
        private AudioSource ambientSource;
        private AudioSource sfxSource;

        // casting sound播放控制
        private bool hasCastingSoundPlayed = false;

        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupAudioSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 订阅Player事件
            Player.OnObjectDetectionComplete += HandleDetectionComplete;
            Player.OnObjectHoldProgress += HandleCastingProgress;
            Player.OnHoldCancelled += HandleCastingCancelled;
        }

        private void OnDisable()
        {
            Player.OnObjectDetectionComplete -= HandleDetectionComplete;
            Player.OnObjectHoldProgress -= HandleCastingProgress;
            Player.OnHoldCancelled -= HandleCastingCancelled;
        }

        private void SetupAudioSources()
        {
            // 创建音乐AudioSource
            GameObject musicGO = new GameObject("Music");
            musicGO.transform.SetParent(transform);
            musicSource = musicGO.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = musicVolume;

            // 创建环境音AudioSource
            GameObject ambientGO = new GameObject("Ambient");
            ambientGO.transform.SetParent(transform);
            ambientSource = ambientGO.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.volume = musicVolume * 0.6f; // 环境音稍微小声点

            // 创建音效AudioSource
            GameObject sfxGO = new GameObject("SFX");
            sfxGO.transform.SetParent(transform);
            sfxSource = sfxGO.AddComponent<AudioSource>();
            sfxSource.volume = sfxVolume;

            Debug.Log("✅ AudioManager: 音频源创建完成");

            // 立即播放菜单音乐测试
            PlayMenuMusic();
        }

        // 背景音乐控制
        public void PlayMenuMusic()
        {
            if (menuMusic != null && musicSource != null)
            {
                musicSource.clip = menuMusic;
                musicSource.Play();
                Debug.Log($"🎵 播放菜单音乐: {menuMusic.name}");
            }
        }

        public void PlayGameMusic()
        {
            if (gameMusic != null && musicSource != null)
            {
                musicSource.clip = gameMusic;
                musicSource.Play();
                Debug.Log($"🎵 播放游戏音乐: {gameMusic.name}");
            }
        }

        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
                Debug.Log("🎵 停止音乐");
            }
        }

        // 环境音控制
        public void PlayAmbientSound()
        {
            if (ambientSound != null && ambientSource != null)
            {
                ambientSource.clip = ambientSound;
                ambientSource.Play();
                Debug.Log($"🌲 播放环境音: {ambientSound.name}");
            }
        }

        public void StopAmbientSound()
        {
            if (ambientSource != null)
            {
                ambientSource.Stop();
                Debug.Log("🌲 停止环境音");
            }
        }

        // 音效播放
        public void PlaySFX(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
                Debug.Log($"🔊 播放音效: {clip.name}");
            }
        }

        // 具体音效方法
        public void PlayCastingSound() => PlaySFX(castingSound);
        public void PlayBugFixSuccessSound() => PlaySFX(bugFixSuccessSound);
        public void PlayBugFixFailSound() => PlaySFX(bugFixFailSound);
        public void PlayButtonClickSound() => PlaySFX(buttonClickSound);
        public void PlayGameStartSound() => PlaySFX(gameStartSound);

        // 音量控制
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null) musicSource.volume = musicVolume;
            if (ambientSource != null) ambientSource.volume = musicVolume * 0.6f;
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        // UI状态响应
        public void OnShowMainMenu()
        {
            PlayMenuMusic();
            StopAmbientSound();
            Debug.Log("🏠 切换到主菜单音频");
        }

        public void OnShowGameHUD()
        {
            PlayGameMusic();
            PlayAmbientSound();
            Debug.Log("🎮 切换到游戏音频");
        }

        public void OnGameStart()
        {
            PlayGameStartSound();
            // 延迟一点播放游戏音乐
            Invoke(nameof(OnShowGameHUD), 0.5f);
            Debug.Log("🎮 游戏开始");
        }

        public void OnReturnToMainMenu()
        {
            OnShowMainMenu();
            Debug.Log("🏠 返回主菜单");
        }

        // 事件处理 - 修复后的casting sound逻辑
        private void HandleDetectionComplete(GameObject obj, bool isBug)
        {
            // 重置casting sound标志，准备下次检测
            hasCastingSoundPlayed = false;

            if (isBug)
            {
                PlayBugFixSuccessSound();
            }
            else
            {
                PlayBugFixFailSound();
            }
        }

        private void HandleCastingProgress(GameObject obj, float progress)
        {
            // 只在刚开始检测时播放一次casting sound
            if (progress > 0f && !hasCastingSoundPlayed)
            {
                PlayCastingSound();
                hasCastingSoundPlayed = true;
                Debug.Log("🪄 播放施法音效（仅一次）");
            }
        }

        private void HandleCastingCancelled()
        {
            // 检测取消时重置标志，准备下次检测
            hasCastingSoundPlayed = false;
            Debug.Log("🪄 施法取消，重置音效标志");
        }

        // 调试用
        [Header("调试")]
        [SerializeField] private bool showDebugPanel = false;

        private void OnGUI()
        {
            if (!showDebugPanel) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 250));
            GUILayout.Label("=== Audio Manager (简化版) ===");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("菜单音乐")) PlayMenuMusic();
            if (GUILayout.Button("游戏音乐")) PlayGameMusic();
            if (GUILayout.Button("停止音乐")) StopMusic();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("环境音")) PlayAmbientSound();
            if (GUILayout.Button("停止环境音")) StopAmbientSound();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("成功音效")) PlayBugFixSuccessSound();
            if (GUILayout.Button("失败音效")) PlayBugFixFailSound();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("施法音效")) PlayCastingSound();
            if (GUILayout.Button("按钮音效")) PlayButtonClickSound();
            GUILayout.EndHorizontal();

            GUILayout.Label($"施法音效已播放: {hasCastingSoundPlayed}");

            if (GUILayout.Button("重置施法音效标志"))
            {
                hasCastingSoundPlayed = false;
            }

            GUILayout.Label($"音乐音量: {musicVolume:F1}");
            musicVolume = GUILayout.HorizontalSlider(musicVolume, 0f, 1f);

            GUILayout.Label($"音效音量: {sfxVolume:F1}");
            sfxVolume = GUILayout.HorizontalSlider(sfxVolume, 0f, 1f);

            if (GUILayout.Button("应用音量"))
            {
                SetMusicVolume(musicVolume);
                SetSFXVolume(sfxVolume);
            }

            GUILayout.EndArea();
        }
    }
}