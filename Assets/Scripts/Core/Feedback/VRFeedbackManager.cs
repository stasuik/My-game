using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RoadBuilder.Core.Feedback
{
    /// <summary>
    /// Менеджер аудио- и тактильной обратной связи (Haptics) для VR.
    /// Воспроизводит undo.wav / redo.wav и вибрацию [50, 30, 50] мс.
    /// Не воспроизводит звук, если стек пуст (только тост).
    /// </summary>
    public class VRFeedbackManager : MonoBehaviour
    {
        private static VRFeedbackManager _instance;
        public static VRFeedbackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VRFeedbackManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[VRFeedbackManager]");
                        _instance = go.AddComponent<VRFeedbackManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Аудиоклипы")]
        public AudioClip undoClip;
        public AudioClip redoClip;
        public AudioSource audioSource;

        [Header("Тактильные настройки")]
        [SerializeField] private float hapticAmplitude = 0.65f;
        private readonly float[] defaultPattern = new float[] { 0.05f, 0.03f, 0.05f }; // [50ms, 30ms pause, 50ms]

        public int SuccessfulAudioPlayCount { get; private set; } = 0;
        public int SuppressedAudioCount { get; private set; } = 0;
        public int HapticTriggerCount { get; private set; } = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            InitializeAudio();
        }

        public void InitializeAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;

            if (undoClip == null) undoClip = Resources.Load<AudioClip>("Audio/undo");
            if (redoClip == null) redoClip = Resources.Load<AudioClip>("Audio/redo");
        }

        public void PlayUndoSuccess()
        {
            SuccessfulAudioPlayCount++;
            PlayClip(undoClip);
            TriggerHapticPulsePattern();
        }

        public void PlayRedoSuccess()
        {
            SuccessfulAudioPlayCount++;
            PlayClip(redoClip);
            TriggerHapticPulsePattern();
        }

        public void PlayEmptyFailure()
        {
            // При пустом стеке звук НЕ воспроизводится (согласно критериям задачи 4.2)
            SuppressedAudioCount++;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        public void TriggerHapticPulsePattern(XRNode node = XRNode.RightHand)
        {
            HapticTriggerCount++;
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(HapticPatternRoutine(node));
            }
        }

        private IEnumerator HapticPatternRoutine(XRNode node)
        {
            // Паттерн [50, 30, 50]: импульс 50мс -> пауза 30мс -> импульс 50мс
            SendHapticPulse(node, hapticAmplitude, defaultPattern[0]);
            yield return new WaitForSecondsRealtime(defaultPattern[1]);
            SendHapticPulse(node, hapticAmplitude, defaultPattern[2]);
        }

        private void SendHapticPulse(XRNode node, float amplitude, float duration)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}