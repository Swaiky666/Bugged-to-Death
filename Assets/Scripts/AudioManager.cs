// AudioManager.cs - 完整的音频管理系统，包含背景音乐和音效控制
// 
// 音效分类说明：
// 1. 背景音乐（开始菜单）- 主菜单界面背景音乐
// 2. 背景音乐（游戏内）- 游戏进行时背景音乐  
// 3. 环境音效（游戏内）- 游戏场景环境音效
// 4. 施法音效 - 玩家开始检测/施法时播放
// 5. 修复成功音效 - 成功修复Bug时播放
// 6. 修复失败音效 - 修复失败时播放
//
using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

namespace BugFixerGame
{
    [System.Serializable]
    public class AudioClipData
    {
        [SerializeField] public string name;
        [SerializeField] public AudioClip clip;
        [SerializeField, Range(0f, 1f)] public float volume = 1f;
        [SerializeField, Range(0.1f, 3f)] public float pitch = 1f;
        [SerializeField] public bool loop = false;
        [SerializeField] public bool is3D = false;
        [SerializeField, Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, 1 = 3D

        public AudioClipData(string audioName, AudioClip audioClip)
        {
            name = audioName;
            clip = audioClip;
            volume = 1f;
            pitch = 1f;
            loop = false;
            is3D = false;
            spatialBlend = 0f;
        }
    }

    public class AudioManager : MonoBehaviour
    {
        [Header("音频混合器设置")]
        [SerializeField] private AudioMixerGroup masterMixerGroup;
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [SerializeField] private AudioMixerGroup ambientMixerGroup;

        [Header("背景音乐设置")]
        [SerializeField] private AudioClipData menuBackgroundMusic;
        [SerializeField] private AudioClipData gameBackgroundMusic;
        [SerializeField] private AudioClipData ambientSound;
        [SerializeField, Range(0f, 5f)] private float musicFadeTime = 2f;

        [Header("音效设置")]
        [SerializeField] private AudioClipData castingSound;                // 施法音效
        [SerializeField] private AudioClipData bugFixSuccessSound;          // 修复Bug成功音效
        [SerializeField] private AudioClipData bugFixFailSound;             // 修复Bug失败音效
        [SerializeField] private AudioClipData buttonClickSound;            // 按钮点击音效
        [SerializeField] private AudioClipData gameStartSound;              // 游戏开始音效

        [Header("音频源池设置")]
        [SerializeField, Range(1, 10)] private int sfxAudioSourcePoolSize = 5;
        [SerializeField] private int maxConcurrentSounds = 10;

        [Header("音量设置")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.5f;

        [Header("高级设置")]
        [SerializeField] private bool enableDopplerEffect = false;
        [SerializeField] private bool muteOnFocusLoss = true;
        [SerializeField] private bool persistVolumeSettings = true;
        [SerializeField] private bool autoDetectAudioListener = true;

        [Header("当前状态显示 (只读)")]
        [SerializeField, ReadOnly] private string currentMusicState = "无";
        [SerializeField, ReadOnly] private string currentAmbientState = "无";
        [SerializeField, ReadOnly] private int activeSFXCount = 0;
        [SerializeField, ReadOnly] private bool isMuted = false;

        // 音频源管理
        private AudioSource musicAudioSource;
        private AudioSource ambientAudioSource;
        private Queue<AudioSource> sfxAudioSourcePool = new Queue<AudioSource>();
        private List<AudioSource> activeSFXSources = new List<AudioSource>();

        // 音频状态管理
        private Coroutine musicFadeCoroutine;
        private Coroutine ambientFadeCoroutine;
        private bool isInitialized = false;
        private AudioListener audioListener;

        // 音量保存键名
        private const string MASTER_VOLUME_KEY = "Audio_MasterVolume";
        private const string MUSIC_VOLUME_KEY = "Audio_MusicVolume";
        private const string SFX_VOLUME_KEY = "Audio_SFXVolume";
        private const string AMBIENT_VOLUME_KEY = "Audio_AmbientVolume";
        private const string MUTE_KEY = "Audio_IsMuted";

        public static AudioManager Instance { get; private set; }

        // 音频事件
        public static event Action<float> OnMasterVolumeChanged;
        public static event Action<float> OnMusicVolumeChanged;
        public static event Action<float> OnSFXVolumeChanged;
        public static event Action<float> OnAmbientVolumeChanged;
        public static event Action<bool> OnMuteStateChanged;
        public static event Action<string> OnMusicChanged;
        public static event Action<string> OnAmbientChanged;

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 订阅游戏事件
            GameManager.OnPauseStateChanged += HandlePauseStateChanged;

            // 订阅Player事件
            Player.OnObjectDetectionComplete += HandleDetectionComplete;
            Player.OnObjectHoldProgress += HandleCastingProgress;

            // 订阅焦点事件
            Application.focusChanged += HandleApplicationFocus;
        }

        private void OnDisable()
        {
            // 取消订阅
            GameManager.OnPauseStateChanged -= HandlePauseStateChanged;

            Player.OnObjectDetectionComplete -= HandleDetectionComplete;
            Player.OnObjectHoldProgress -= HandleCastingProgress;

            Application.focusChanged -= HandleApplicationFocus;

            // 保存音量设置
            if (persistVolumeSettings)
            {
                SaveVolumeSettings();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (muteOnFocusLoss)
            {
                HandleApplicationFocus(!pauseStatus);
            }
        }

        #endregion

        #region 初始化

        private void InitializeAudioManager()
        {
            Debug.Log("🎵 AudioManager: 开始初始化音频系统");

            // 自动检测AudioListener
            if (autoDetectAudioListener)
            {
                FindAudioListener();
            }

            // 创建音频源
            CreateAudioSources();

            // 创建音效源池
            CreateSFXAudioSourcePool();

            // 加载保存的音量设置
            if (persistVolumeSettings)
            {
                LoadVolumeSettings();
            }

            // 应用初始音量设置
            ApplyVolumeSettings();

            isInitialized = true;
            Debug.Log("✅ AudioManager: 音频系统初始化完成");
        }

        private void FindAudioListener()
        {
            audioListener = FindObjectOfType<AudioListener>();
            if (audioListener == null)
            {
                Debug.LogWarning("⚠️ AudioManager: 未找到AudioListener，某些功能可能受限");
            }
            else
            {
                Debug.Log($"🎧 AudioManager: 找到AudioListener在 {audioListener.gameObject.name}");
            }
        }

        private void CreateAudioSources()
        {
            // 创建背景音乐音频源
            GameObject musicGO = new GameObject("Music AudioSource");
            musicGO.transform.SetParent(transform);
            musicAudioSource = musicGO.AddComponent<AudioSource>();
            ConfigureAudioSource(musicAudioSource, musicMixerGroup, true, false);

            // 创建环境音效音频源
            GameObject ambientGO = new GameObject("Ambient AudioSource");
            ambientGO.transform.SetParent(transform);
            ambientAudioSource = ambientGO.AddComponent<AudioSource>();
            ConfigureAudioSource(ambientAudioSource, ambientMixerGroup, true, false);

            Debug.Log("🎼 AudioManager: 背景音乐和环境音效音频源创建完成");
        }

        private void CreateSFXAudioSourcePool()
        {
            sfxAudioSourcePool.Clear();

            for (int i = 0; i < sfxAudioSourcePoolSize; i++)
            {
                GameObject sfxGO = new GameObject($"SFX AudioSource {i + 1}");
                sfxGO.transform.SetParent(transform);
                AudioSource sfxSource = sfxGO.AddComponent<AudioSource>();
                ConfigureAudioSource(sfxSource, sfxMixerGroup, false, false);
                sfxAudioSourcePool.Enqueue(sfxSource);
            }

            Debug.Log($"🔊 AudioManager: 音效源池创建完成，数量: {sfxAudioSourcePoolSize}");
        }

        private void ConfigureAudioSource(AudioSource source, AudioMixerGroup mixerGroup, bool loop, bool playOnAwake)
        {
            source.outputAudioMixerGroup = mixerGroup;
            source.loop = loop;
            source.playOnAwake = playOnAwake;
            source.spatialBlend = 0f; // 默认2D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = enableDopplerEffect ? 1f : 0f;
        }

        #endregion

        #region 背景音乐控制

        /// <summary>
        /// 播放菜单背景音乐
        /// </summary>
        public void PlayMenuMusic()
        {
            if (menuBackgroundMusic?.clip != null)
            {
                PlayMusic(menuBackgroundMusic, "菜单音乐");
            }
            else
            {
                Debug.LogWarning("⚠️ AudioManager: 菜单背景音乐未设置");
            }
        }

        /// <summary>
        /// 播放游戏背景音乐
        /// </summary>
        public void PlayGameMusic()
        {
            if (gameBackgroundMusic?.clip != null)
            {
                PlayMusic(gameBackgroundMusic, "游戏音乐");
            }
            else
            {
                Debug.LogWarning("⚠️ AudioManager: 游戏背景音乐未设置");
            }
        }

        /// <summary>
        /// 播放指定的背景音乐
        /// </summary>
        private void PlayMusic(AudioClipData musicData, string musicName)
        {
            if (!isInitialized || musicAudioSource == null || musicData?.clip == null)
            {
                Debug.LogWarning($"⚠️ AudioManager: 无法播放{musicName} - 系统未初始化或音频数据无效");
                return;
            }

            // 如果当前已经在播放相同的音乐，则不重复播放
            if (musicAudioSource.clip == musicData.clip && musicAudioSource.isPlaying)
            {
                Debug.Log($"🎵 AudioManager: {musicName}已在播放，跳过");
                return;
            }

            // 停止当前淡入淡出协程
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            // 开始淡入淡出切换音乐
            musicFadeCoroutine = StartCoroutine(FadeMusicCoroutine(musicData, musicName));
        }

        /// <summary>
        /// 音乐淡入淡出切换协程
        /// </summary>
        private IEnumerator FadeMusicCoroutine(AudioClipData newMusicData, string musicName)
        {
            currentMusicState = $"切换到{musicName}";

            // 第一阶段：淡出当前音乐
            if (musicAudioSource.isPlaying)
            {
                float startVolume = musicAudioSource.volume;
                float elapsed = 0f;

                while (elapsed < musicFadeTime / 2f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeTime / 2f));
                    yield return null;
                }

                musicAudioSource.Stop();
            }

            // 第二阶段：设置新音乐并淡入
            musicAudioSource.clip = newMusicData.clip;
            musicAudioSource.volume = 0f;
            musicAudioSource.pitch = newMusicData.pitch;
            musicAudioSource.loop = newMusicData.loop;
            musicAudioSource.Play();

            float targetVolume = newMusicData.volume * musicVolume * masterVolume;
            float elapsed2 = 0f;

            while (elapsed2 < musicFadeTime / 2f)
            {
                elapsed2 += Time.unscaledDeltaTime;
                musicAudioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed2 / (musicFadeTime / 2f));
                yield return null;
            }

            musicAudioSource.volume = targetVolume;
            currentMusicState = musicName;
            OnMusicChanged?.Invoke(musicName);

            Debug.Log($"🎵 AudioManager: {musicName}切换完成");
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public void StopMusic()
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            musicFadeCoroutine = StartCoroutine(FadeOutMusicCoroutine());
        }

        /// <summary>
        /// 音乐淡出协程
        /// </summary>
        private IEnumerator FadeOutMusicCoroutine()
        {
            if (musicAudioSource == null || !musicAudioSource.isPlaying) yield break;

            currentMusicState = "停止中";
            float startVolume = musicAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeTime);
                yield return null;
            }

            musicAudioSource.Stop();
            musicAudioSource.volume = startVolume;
            currentMusicState = "无";
            OnMusicChanged?.Invoke("无");

            Debug.Log("🎵 AudioManager: 背景音乐已停止");
        }

        #endregion

        #region 环境音效控制

        /// <summary>
        /// 播放游戏内环境音效
        /// </summary>
        public void PlayAmbientSound()
        {
            if (ambientSound?.clip != null)
            {
                PlayAmbient(ambientSound, "游戏环境音");
            }
            else
            {
                Debug.LogWarning("⚠️ AudioManager: 游戏环境音效未设置");
            }
        }

        /// <summary>
        /// 播放指定的环境音效
        /// </summary>
        private void PlayAmbient(AudioClipData ambientData, string ambientName)
        {
            if (!isInitialized || ambientAudioSource == null || ambientData?.clip == null)
            {
                Debug.LogWarning($"⚠️ AudioManager: 无法播放{ambientName} - 系统未初始化或音频数据无效");
                return;
            }

            // 如果当前已经在播放相同的环境音，则不重复播放
            if (ambientAudioSource.clip == ambientData.clip && ambientAudioSource.isPlaying)
            {
                Debug.Log($"🌲 AudioManager: {ambientName}已在播放，跳过");
                return;
            }

            // 停止当前淡入淡出协程
            if (ambientFadeCoroutine != null)
            {
                StopCoroutine(ambientFadeCoroutine);
            }

            // 开始淡入淡出切换环境音
            ambientFadeCoroutine = StartCoroutine(FadeAmbientCoroutine(ambientData, ambientName));
        }

        /// <summary>
        /// 环境音淡入淡出切换协程
        /// </summary>
        private IEnumerator FadeAmbientCoroutine(AudioClipData newAmbientData, string ambientName)
        {
            currentAmbientState = $"切换到{ambientName}";

            // 第一阶段：淡出当前环境音
            if (ambientAudioSource.isPlaying)
            {
                float startVolume = ambientAudioSource.volume;
                float elapsed = 0f;

                while (elapsed < musicFadeTime / 2f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    ambientAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeTime / 2f));
                    yield return null;
                }

                ambientAudioSource.Stop();
            }

            // 第二阶段：设置新环境音并淡入
            ambientAudioSource.clip = newAmbientData.clip;
            ambientAudioSource.volume = 0f;
            ambientAudioSource.pitch = newAmbientData.pitch;
            ambientAudioSource.loop = newAmbientData.loop;
            ambientAudioSource.Play();

            float targetVolume = newAmbientData.volume * ambientVolume * masterVolume;
            float elapsed2 = 0f;

            while (elapsed2 < musicFadeTime / 2f)
            {
                elapsed2 += Time.unscaledDeltaTime;
                ambientAudioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed2 / (musicFadeTime / 2f));
                yield return null;
            }

            ambientAudioSource.volume = targetVolume;
            currentAmbientState = ambientName;
            OnAmbientChanged?.Invoke(ambientName);

            Debug.Log($"🌲 AudioManager: {ambientName}切换完成");
        }

        /// <summary>
        /// 停止环境音效
        /// </summary>
        public void StopAmbientSound()
        {
            if (ambientFadeCoroutine != null)
            {
                StopCoroutine(ambientFadeCoroutine);
            }

            ambientFadeCoroutine = StartCoroutine(FadeOutAmbientCoroutine());
        }

        /// <summary>
        /// 环境音淡出协程
        /// </summary>
        private IEnumerator FadeOutAmbientCoroutine()
        {
            if (ambientAudioSource == null || !ambientAudioSource.isPlaying) yield break;

            currentAmbientState = "停止中";
            float startVolume = ambientAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                ambientAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeTime);
                yield return null;
            }

            ambientAudioSource.Stop();
            ambientAudioSource.volume = startVolume;
            currentAmbientState = "无";
            OnAmbientChanged?.Invoke("无");

            Debug.Log("🌲 AudioManager: 环境音效已停止");
        }

        #endregion

        #region 音效播放

        /// <summary>
        /// 播放音效
        /// </summary>
        public void PlaySFX(AudioClipData sfxData, Vector3? position = null, Transform parent = null)
        {
            if (!isInitialized || sfxData?.clip == null)
            {
                Debug.LogWarning("⚠️ AudioManager: 无法播放音效 - 系统未初始化或音效数据无效");
                return;
            }

            // 检查并发音效数量限制
            if (activeSFXSources.Count >= maxConcurrentSounds)
            {
                Debug.LogWarning($"⚠️ AudioManager: 达到最大并发音效数量限制({maxConcurrentSounds})");
                return;
            }

            AudioSource sfxSource = GetSFXAudioSource();
            if (sfxSource == null)
            {
                Debug.LogWarning("⚠️ AudioManager: 无可用的音效源");
                return;
            }

            // 配置音效源
            ConfigureSFXAudioSource(sfxSource, sfxData, position, parent);

            // 播放音效
            sfxSource.Play();
            activeSFXSources.Add(sfxSource);
            UpdateActiveSFXCount();

            // 启动协程来处理音效播放完成后的清理
            StartCoroutine(HandleSFXCompleteCoroutine(sfxSource, sfxData.name));

            Debug.Log($"🔊 AudioManager: 播放音效 {sfxData.name}");
        }

        /// <summary>
        /// 播放指定的游戏音效
        /// </summary>
        public void PlayCastingSound() => PlaySFX(castingSound);                    // 施法音效
        public void PlayBugFixSuccessSound() => PlaySFX(bugFixSuccessSound);        // 修复Bug成功音效  
        public void PlayBugFixFailSound() => PlaySFX(bugFixFailSound);              // 修复Bug失败音效
        public void PlayButtonClickSound() => PlaySFX(buttonClickSound);            // 按钮点击音效
        public void PlayGameStartSound() => PlaySFX(gameStartSound);                // 游戏开始音效

        /// <summary>
        /// 播放3D位置音效
        /// </summary>
        public void PlaySFX3D(AudioClipData sfxData, Vector3 position, float maxDistance = 50f)
        {
            if (!isInitialized || sfxData?.clip == null) return;

            AudioSource sfxSource = GetSFXAudioSource();
            if (sfxSource == null) return;

            // 配置为3D音效
            sfxSource.clip = sfxData.clip;
            sfxSource.volume = sfxData.volume * sfxVolume * masterVolume;
            sfxSource.pitch = sfxData.pitch;
            sfxSource.loop = sfxData.loop;
            sfxSource.spatialBlend = 1f; // 完全3D
            sfxSource.maxDistance = maxDistance;
            sfxSource.transform.position = position;

            sfxSource.Play();
            activeSFXSources.Add(sfxSource);
            UpdateActiveSFXCount();

            StartCoroutine(HandleSFXCompleteCoroutine(sfxSource, sfxData.name + " (3D)"));
        }

        /// <summary>
        /// 获取可用的音效源
        /// </summary>
        private AudioSource GetSFXAudioSource()
        {
            // 首先清理已完成的音效源
            CleanupCompletedSFX();

            // 尝试从池中获取
            if (sfxAudioSourcePool.Count > 0)
            {
                return sfxAudioSourcePool.Dequeue();
            }

            // 如果池为空，尝试重用最老的活跃音效源
            if (activeSFXSources.Count > 0)
            {
                AudioSource oldestSource = activeSFXSources[0];
                oldestSource.Stop();
                activeSFXSources.RemoveAt(0);
                return oldestSource;
            }

            // 作为最后手段，创建新的音效源（不应该到达这里）
            Debug.LogWarning("⚠️ AudioManager: 创建额外的音效源");
            GameObject sfxGO = new GameObject("Emergency SFX AudioSource");
            sfxGO.transform.SetParent(transform);
            AudioSource newSource = sfxGO.AddComponent<AudioSource>();
            ConfigureAudioSource(newSource, sfxMixerGroup, false, false);
            return newSource;
        }

        /// <summary>
        /// 配置音效源
        /// </summary>
        private void ConfigureSFXAudioSource(AudioSource source, AudioClipData sfxData, Vector3? position, Transform parent)
        {
            source.clip = sfxData.clip;
            source.volume = sfxData.volume * sfxVolume * masterVolume;
            source.pitch = sfxData.pitch;
            source.loop = sfxData.loop;
            source.spatialBlend = sfxData.spatialBlend;

            if (sfxData.is3D && position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
            }
            else
            {
                source.spatialBlend = 0f; // 2D音效
            }

            if (parent != null)
            {
                source.transform.SetParent(parent);
            }
            else
            {
                source.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// 处理音效播放完成的协程
        /// </summary>
        private IEnumerator HandleSFXCompleteCoroutine(AudioSource source, string sfxName)
        {
            // 等待音效播放完成
            yield return new WaitWhile(() => source.isPlaying);

            // 归还到池中
            ReturnSFXToPool(source, sfxName);
        }

        /// <summary>
        /// 将音效源归还到池中
        /// </summary>
        private void ReturnSFXToPool(AudioSource source, string sfxName)
        {
            if (source == null) return;

            // 从活跃列表中移除
            activeSFXSources.Remove(source);
            UpdateActiveSFXCount();

            // 重置音效源状态
            source.clip = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
            source.spatialBlend = 0f;
            source.transform.SetParent(transform);
            source.transform.localPosition = Vector3.zero;

            // 归还到池中
            sfxAudioSourcePool.Enqueue(source);

            Debug.Log($"♻️ AudioManager: 音效 {sfxName} 播放完成并归还到池");
        }

        /// <summary>
        /// 清理已完成的音效
        /// </summary>
        private void CleanupCompletedSFX()
        {
            for (int i = activeSFXSources.Count - 1; i >= 0; i--)
            {
                if (activeSFXSources[i] == null || !activeSFXSources[i].isPlaying)
                {
                    AudioSource source = activeSFXSources[i];
                    activeSFXSources.RemoveAt(i);

                    if (source != null)
                    {
                        ReturnSFXToPool(source, "清理的音效");
                    }
                }
            }
            UpdateActiveSFXCount();
        }

        /// <summary>
        /// 停止所有音效
        /// </summary>
        public void StopAllSFX()
        {
            for (int i = activeSFXSources.Count - 1; i >= 0; i--)
            {
                if (activeSFXSources[i] != null)
                {
                    activeSFXSources[i].Stop();
                }
            }

            CleanupCompletedSFX();
            Debug.Log("🔇 AudioManager: 所有音效已停止");
        }

        /// <summary>
        /// 更新活跃音效计数
        /// </summary>
        private void UpdateActiveSFXCount()
        {
            activeSFXCount = activeSFXSources.Count;
        }

        #endregion

        #region 音量控制

        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            ApplyVolumeSettings();
            OnMasterVolumeChanged?.Invoke(masterVolume);
            Debug.Log($"🔊 AudioManager: 主音量设置为 {masterVolume:P0}");
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
            OnMusicVolumeChanged?.Invoke(musicVolume);
            Debug.Log($"🎵 AudioManager: 音乐音量设置为 {musicVolume:P0}");
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            ApplySFXVolume();
            OnSFXVolumeChanged?.Invoke(sfxVolume);
            Debug.Log($"🔊 AudioManager: 音效音量设置为 {sfxVolume:P0}");
        }

        /// <summary>
        /// 设置环境音效音量
        /// </summary>
        public void SetAmbientVolume(float volume)
        {
            ambientVolume = Mathf.Clamp01(volume);
            ApplyAmbientVolume();
            OnAmbientVolumeChanged?.Invoke(ambientVolume);
            Debug.Log($"🌲 AudioManager: 环境音量设置为 {ambientVolume:P0}");
        }

        /// <summary>
        /// 应用所有音量设置
        /// </summary>
        private void ApplyVolumeSettings()
        {
            ApplyMusicVolume();
            ApplyAmbientVolume();
            ApplySFXVolume();
        }

        /// <summary>
        /// 应用音乐音量
        /// </summary>
        private void ApplyMusicVolume()
        {
            if (musicAudioSource != null && musicAudioSource.clip != null)
            {
                float targetVolume = GetMusicDataVolume() * musicVolume * masterVolume;
                if (!isMuted)
                {
                    musicAudioSource.volume = targetVolume;
                }
            }
        }

        /// <summary>
        /// 应用环境音量
        /// </summary>
        private void ApplyAmbientVolume()
        {
            if (ambientAudioSource != null && ambientAudioSource.clip != null)
            {
                float targetVolume = GetAmbientDataVolume() * ambientVolume * masterVolume;
                if (!isMuted)
                {
                    ambientAudioSource.volume = targetVolume;
                }
            }
        }

        /// <summary>
        /// 应用音效音量
        /// </summary>
        private void ApplySFXVolume()
        {
            foreach (var source in activeSFXSources)
            {
                if (source != null && source.clip != null)
                {
                    // 获取原始音效数据音量（这里简化处理，使用当前音量比例）
                    float originalDataVolume = source.volume / (sfxVolume * masterVolume);
                    float targetVolume = originalDataVolume * sfxVolume * masterVolume;
                    if (!isMuted)
                    {
                        source.volume = targetVolume;
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前音乐数据的音量
        /// </summary>
        private float GetMusicDataVolume()
        {
            if (musicAudioSource.clip == menuBackgroundMusic?.clip)
                return menuBackgroundMusic.volume;
            if (musicAudioSource.clip == gameBackgroundMusic?.clip)
                return gameBackgroundMusic.volume;
            return 1f;
        }

        /// <summary>
        /// 获取当前环境音数据的音量
        /// </summary>
        private float GetAmbientDataVolume()
        {
            if (ambientAudioSource.clip == ambientSound?.clip)
                return ambientSound.volume;
            return 1f;
        }

        /// <summary>
        /// 静音/取消静音
        /// </summary>
        public void SetMuted(bool muted)
        {
            isMuted = muted;

            if (isMuted)
            {
                // 静音所有音频
                if (musicAudioSource != null) musicAudioSource.volume = 0f;
                if (ambientAudioSource != null) ambientAudioSource.volume = 0f;
                foreach (var source in activeSFXSources)
                {
                    if (source != null) source.volume = 0f;
                }
            }
            else
            {
                // 恢复音量
                ApplyVolumeSettings();
            }

            OnMuteStateChanged?.Invoke(isMuted);
            Debug.Log($"🔇 AudioManager: {(isMuted ? "静音" : "取消静音")}");
        }

        /// <summary>
        /// 切换静音状态
        /// </summary>
        public void ToggleMute()
        {
            SetMuted(!isMuted);
        }

        #endregion

        #region 音量设置持久化

        /// <summary>
        /// 保存音量设置
        /// </summary>
        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolume);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolume);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);
            PlayerPrefs.SetFloat(AMBIENT_VOLUME_KEY, ambientVolume);
            PlayerPrefs.SetInt(MUTE_KEY, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            Debug.Log("💾 AudioManager: 音量设置已保存");
        }

        /// <summary>
        /// 加载音量设置
        /// </summary>
        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, masterVolume);
            musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, sfxVolume);
            ambientVolume = PlayerPrefs.GetFloat(AMBIENT_VOLUME_KEY, ambientVolume);
            isMuted = PlayerPrefs.GetInt(MUTE_KEY, 0) == 1;

            Debug.Log("📁 AudioManager: 音量设置已加载");
        }

        /// <summary>
        /// 重置音量设置为默认值
        /// </summary>
        public void ResetVolumeSettings()
        {
            masterVolume = 1f;
            musicVolume = 0.7f;
            sfxVolume = 0.8f;
            ambientVolume = 0.5f;
            isMuted = false;

            ApplyVolumeSettings();

            if (persistVolumeSettings)
            {
                SaveVolumeSettings();
            }

            Debug.Log("🔄 AudioManager: 音量设置已重置为默认值");
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理游戏暂停状态变化
        /// </summary>
        private void HandlePauseStateChanged(bool isPaused)
        {
            // 暂停时降低音量，恢复时恢复音量
            float volumeMultiplier = isPaused ? 0.3f : 1f;

            if (musicAudioSource != null)
            {
                musicAudioSource.volume *= volumeMultiplier;
            }

            if (ambientAudioSource != null)
            {
                ambientAudioSource.volume *= volumeMultiplier;
            }

            Debug.Log($"⏸️ AudioManager: 游戏{(isPaused ? "暂停" : "恢复")}，音量调整为 {volumeMultiplier:P0}");
        }

        /// <summary>
        /// 处理应用程序焦点变化
        /// </summary>
        private void HandleApplicationFocus(bool hasFocus)
        {
            if (!muteOnFocusLoss) return;

            if (!hasFocus)
            {
                // 失去焦点时静音
                AudioListener.pause = true;
                Debug.Log("🔇 AudioManager: 应用失去焦点，音频暂停");
            }
            else
            {
                // 获得焦点时恢复
                AudioListener.pause = false;
                Debug.Log("🔊 AudioManager: 应用获得焦点，音频恢复");
            }
        }

        /// <summary>
        /// 处理检测完成事件
        /// </summary>
        private void HandleDetectionComplete(GameObject obj, bool isBug)
        {
            if (isBug)
            {
                PlayBugFixSuccessSound(); // 修复Bug成功
            }
            else
            {
                PlayBugFixFailSound(); // 修复失败
            }
        }

        /// <summary>
        /// 处理施法进度事件
        /// </summary>
        private void HandleCastingProgress(GameObject obj, float progress)
        {
            // 当开始施法时播放施法音效（只播放一次）
            if (progress > 0f && progress < 0.1f)
            {
                PlayCastingSound();
            }
        }

        #endregion

        #region 场景音频管理

        /// <summary>
        /// 切换到主菜单音频
        /// </summary>
        public void SwitchToMenuAudio()
        {
            PlayMenuMusic();
            StopAmbientSound();
            Debug.Log("🏠 AudioManager: 切换到主菜单音频");
        }

        /// <summary>
        /// 切换到游戏内音频
        /// </summary>
        public void SwitchToGameAudio()
        {
            PlayGameMusic();
            PlayAmbientSound();
            Debug.Log("🎮 AudioManager: 切换到游戏内音频");
        }

        /// <summary>
        /// 停止所有音频
        /// </summary>
        public void StopAllAudio()
        {
            StopMusic();
            StopAmbientSound();
            StopAllSFX();
            Debug.Log("🔇 AudioManager: 所有音频已停止");
        }

        #endregion

        #region 公共接口

        // 音量获取器
        public float GetMasterVolume() => masterVolume;
        public float GetMusicVolume() => musicVolume;
        public float GetSFXVolume() => sfxVolume;
        public float GetAmbientVolume() => ambientVolume;
        public bool IsMuted() => isMuted;

        // 状态获取器
        public bool IsMusicPlaying() => musicAudioSource != null && musicAudioSource.isPlaying;
        public bool IsAmbientPlaying() => ambientAudioSource != null && ambientAudioSource.isPlaying;
        public string GetCurrentMusicState() => currentMusicState;
        public string GetCurrentAmbientState() => currentAmbientState;
        public int GetActiveSFXCount() => activeSFXCount;

        // 音频池状态
        public int GetAvailableSFXSources() => sfxAudioSourcePool.Count;
        public int GetTotalSFXSources() => sfxAudioSourcePoolSize;

        /// <summary>
        /// 获取音频系统状态信息
        /// </summary>
        public string GetAudioSystemStatus()
        {
            return $"音频系统状态:\n" +
                   $"初始化: {isInitialized}\n" +
                   $"当前音乐: {currentMusicState}\n" +
                   $"当前环境音: {currentAmbientState}\n" +
                   $"活跃音效: {activeSFXCount}/{maxConcurrentSounds}\n" +
                   $"可用音效源: {sfxAudioSourcePool.Count}/{sfxAudioSourcePoolSize}\n" +
                   $"主音量: {masterVolume:P0}\n" +
                   $"音乐音量: {musicVolume:P0}\n" +
                   $"音效音量: {sfxVolume:P0}\n" +
                   $"环境音量: {ambientVolume:P0}\n" +
                   $"静音状态: {isMuted}";
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 420, 400, 410));
            GUILayout.Label("=== AudioManager 调试 ===");

            // 状态信息
            GUILayout.Label($"初始化: {isInitialized}");
            GUILayout.Label($"当前音乐: {currentMusicState}");
            GUILayout.Label($"当前环境音: {currentAmbientState}");
            GUILayout.Label($"活跃音效: {activeSFXCount}/{maxConcurrentSounds}");
            GUILayout.Label($"可用音效源: {sfxAudioSourcePool.Count}");

            GUILayout.Space(5);

            // 音量信息
            GUILayout.Label($"主音量: {masterVolume:P0}");
            GUILayout.Label($"音乐: {musicVolume:P0} | 音效: {sfxVolume:P0} | 环境: {ambientVolume:P0}");
            GUILayout.Label($"静音: {isMuted}");

            GUILayout.Space(10);

            // 音乐控制
            GUILayout.Label("=== 音乐控制 ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("菜单音乐")) PlayMenuMusic();
            if (GUILayout.Button("游戏音乐")) PlayGameMusic();
            if (GUILayout.Button("停止音乐")) StopMusic();
            GUILayout.EndHorizontal();

            // 环境音控制
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("播放环境音")) PlayAmbientSound();
            if (GUILayout.Button("停止环境音")) StopAmbientSound();
            GUILayout.EndHorizontal();

            // 音效测试
            GUILayout.Label("=== 音效测试 ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("施法")) PlayCastingSound();
            if (GUILayout.Button("修复成功")) PlayBugFixSuccessSound();
            if (GUILayout.Button("修复失败")) PlayBugFixFailSound();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("按钮")) PlayButtonClickSound();
            if (GUILayout.Button("开始")) PlayGameStartSound();
            GUILayout.EndHorizontal();

            // 音量控制
            GUILayout.Label("=== 音量控制 ===");
            masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);
            GUILayout.Label($"主音量: {masterVolume:P0}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("静音")) ToggleMute();
            if (GUILayout.Button("重置音量")) ResetVolumeSettings();
            if (GUILayout.Button("停止所有")) StopAllAudio();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        [ContextMenu("🎵 播放菜单音乐")]
        private void TestPlayMenuMusic() => PlayMenuMusic();

        [ContextMenu("🎮 播放游戏音乐")]
        private void TestPlayGameMusic() => PlayGameMusic();

        [ContextMenu("🌲 播放环境音")]
        private void TestPlayAmbientSound() => PlayAmbientSound();

        [ContextMenu("🔊 测试所有音效")]
        private void TestAllSFX()
        {
            if (!Application.isPlaying) return;

            StartCoroutine(TestAllSFXCoroutine());
        }

        private IEnumerator TestAllSFXCoroutine()
        {
            PlayCastingSound();
            yield return new WaitForSeconds(0.5f);

            PlayBugFixSuccessSound();
            yield return new WaitForSeconds(0.5f);

            PlayBugFixFailSound();
            yield return new WaitForSeconds(0.5f);

            PlayButtonClickSound();
            yield return new WaitForSeconds(0.5f);

            PlayGameStartSound();
        }

        [ContextMenu("🔇 切换静音")]
        private void TestToggleMute() => ToggleMute();

        [ContextMenu("🔄 重置音量设置")]
        private void TestResetVolumeSettings() => ResetVolumeSettings();

        [ContextMenu("📊 显示音频状态")]
        private void ShowAudioStatus()
        {
            Debug.Log(GetAudioSystemStatus());
        }

        [ContextMenu("🧹 清理音效源")]
        private void TestCleanupSFX()
        {
            if (Application.isPlaying)
            {
                CleanupCompletedSFX();
                Debug.Log("🧹 音效源清理完成");
            }
        }

        [ContextMenu("🏠 切换到菜单音频")]
        private void TestSwitchToMenuAudio() => SwitchToMenuAudio();

        [ContextMenu("🎮 切换到游戏音频")]
        private void TestSwitchToGameAudio() => SwitchToGameAudio();

        [ContextMenu("💾 保存音量设置")]
        private void TestSaveVolumeSettings()
        {
            if (persistVolumeSettings)
            {
                SaveVolumeSettings();
            }
        }

        [ContextMenu("📁 加载音量设置")]
        private void TestLoadVolumeSettings()
        {
            if (persistVolumeSettings)
            {
                LoadVolumeSettings();
                ApplyVolumeSettings();
            }
        }

        #endregion
    }
}