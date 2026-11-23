using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioCatalog<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        #if UNITY_EDITOR

        private int LogInvalidEntries()
        {
            int invalidAmount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                if (!IsEntryValid(i))
                    invalidAmount++;
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                if (!IsMusicTrackValid(i))
                    invalidAmount++;
            }

            return invalidAmount;
        }

        private bool IsMusicTrackValid(int index)
        {
            MusicTrack<TMusic> track = tracks[index];
            string label = $"Music track #{index} ({track.Id})";

            if (EqualityComparer<TMusic>.Default.Equals(track.Id, default))
            {
                Debug.LogError($"[AudioCatalog]: {label} has no Id (unassigned).", this);
                return false;
            }

            if (!Enum.IsDefined(typeof(TMusic), track.Id))
            {
                Debug.LogError($"[AudioCatalog]: {label} is no member of {typeof(TMusic).Name}.", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (EqualityComparer<TMusic>.Default.Equals(tracks[j].Id, track.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {track.Id} of Music track #{j}. BuildTables throws at the host's Awake.", this);
                    return false;
                }
            }

            if (track.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }

        private bool IsEntryValid(int index)
        {
            AudioEntry<TSfx> entry = entries[index];
            string label = $"Sfx entry #{index} ({entry.Id})";

            if (EqualityComparer<TSfx>.Default.Equals(entry.Id, default))
            {
                Debug.LogError($"[AudioCatalog]: {label} has no Id (unassigned).", this);
                return false;
            }

            if (!Enum.IsDefined(typeof(TSfx), entry.Id))
            {
                Debug.LogError($"[AudioCatalog]: {label} is no member of {typeof(TSfx).Name}.", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (EqualityComparer<TSfx>.Default.Equals(entries[j].Id, entry.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {entry.Id} of Sfx entry #{j}. BuildTables throws at the host's Awake.", this);
                    return false;
                }
            }

            if (entry.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }
        
        /// <summary>Id của enum chưa có entry, track: hợp lệ khi clip chưa về, nhưng phát id đó chỉ log lỗi và im. Trả số id thiếu.</summary>
        private int LogMissingIds()
        {
            var sfxIds = new List<TSfx>(entries.Length);
            foreach (AudioEntry<TSfx> entry in entries)
                sfxIds.Add(entry.Id);

            var musicIds = new List<TMusic>(tracks.Length);
            foreach (MusicTrack<TMusic> track in tracks)
                musicIds.Add(track.Id);

            return LogMissingIds("Sfx", sfxIds) + LogMissingIds("Music", musicIds);
        }

        private int LogMissingIds<TId>(string table, List<TId> presentIds) where TId : struct, Enum
        {
            var missing = new List<string>();
            foreach (TId id in Enum.GetValues(typeof(TId)))
            {
                if (!EqualityComparer<TId>.Default.Equals(id, default) && !presentIds.Contains(id))
                    missing.Add(id.ToString());
            }

            if (missing.Count > 0)
                Debug.LogWarning($"[AudioCatalog]: {table} ids without an entry: {string.Join(", ", missing)}. Playing them logs an error and stays silent.", this);

            return missing.Count;
        }

        [Button]
        private void ValidateEntries()
        {
            int invalidAmount = LogInvalidEntries();
            LogMissingIds();

            if (invalidAmount == 0)
                Debug.Log($"[AudioCatalog]: {entries.Length} Sfx entries, {tracks.Length} Music tracks — ids unique and assigned, every slot has a clip.", this);
        }

        // Tiếng phát ra thấp hơn trung vị cả catalog chừng này dB thì đặt cạnh tiếng khác gần như không nghe thấy.
        private const float QUIET_MARGIN_DB = 12f;

        /// <summary>Đỉnh, RMS của từng clip và đỉnh lúc phát (nhân volume của entry); gắn cờ clip nhỏ hẳn so với trung vị.</summary>
        [Button]
        private void AuditLoudness()
        {
            var rows = new List<(string label, AudioClip clip, float volume)>(entries.Length + tracks.Length);
            foreach (AudioEntry<TSfx> entry in entries)
                rows.Add(($"Sfx {entry.Id}", entry.Clip, entry.Volume));
            foreach (MusicTrack<TMusic> track in tracks)
                rows.Add(($"Music {track.Id}", track.Clip, track.Volume));

            var lines = new string[rows.Count];
            var playedPeakDbs = new float[rows.Count];
            var measuredDbs = new List<float>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                (string label, AudioClip clip, float volume) = rows[i];
                playedPeakDbs[i] = float.NaN;
                if (!TryMeasureClip(clip, out float peak, out float rms, out string reason))
                {
                    lines[i] = $"{label}: {reason}";
                    continue;
                }

                playedPeakDbs[i] = ToDecibels(peak * volume);
                measuredDbs.Add(playedPeakDbs[i]);
                lines[i] = $"{label} ({clip.name}): peak {ToDecibels(peak):F1}, RMS {ToDecibels(rms):F1}, played peak x{volume:0.##} {playedPeakDbs[i]:F1} dBFS";
            }

            measuredDbs.Sort();
            float medianDb = measuredDbs.Count > 0 ? measuredDbs[measuredDbs.Count / 2] : 0f;
            var report = new StringBuilder($"[AudioCatalog]: loudness of {measuredDbs.Count}/{rows.Count} clips, median played peak {medianDb:F1} dBFS, QUIET = {QUIET_MARGIN_DB} dB or more under it.\n");
            for (int i = 0; i < rows.Count; i++)
            {
                report.Append(lines[i]);
                if (playedPeakDbs[i] < medianDb - QUIET_MARGIN_DB)
                    report.Append("  <- QUIET");
                report.Append('\n');
            }

            Debug.Log(report.ToString(), this);
        }

        /// <summary>Đỉnh và RMS biên độ trên toàn clip. Chỉ đọc được mẫu thật khi Load Type là Decompress On Load.</summary>
        private static bool TryMeasureClip(AudioClip clip, out float peak, out float rms, out string reason)
        {
            peak = 0f;
            rms = 0f;
            if (clip == null)
            {
                reason = "no clip";
                return false;
            }

            // Load Type khác thì GetData trả mảng toàn 0: đo ra im lặng giả.
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                reason = $"{clip.name}: Load Type {clip.loadType}, switch to Decompress On Load to measure";
                return false;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();

            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                reason = $"{clip.name}: GetData failed (load state {clip.loadState})";
                return false;
            }

            double sumOfSquares = 0;
            foreach (float sample in samples)
            {
                float amplitude = Mathf.Abs(sample);
                if (amplitude > peak)
                    peak = amplitude;
                sumOfSquares += sample * sample;
            }

            rms = samples.Length > 0 ? (float)Math.Sqrt(sumOfSquares / samples.Length) : 0f;
            reason = null;
            return true;
        }

        private static float ToDecibels(float amplitude) => amplitude > 0f ? 20f * Mathf.Log10(amplitude) : float.NegativeInfinity;

        #endif
    }
}