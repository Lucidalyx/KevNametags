using BepInEx;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace KevsNametags
{
    [BepInPlugin("org.kevsmod.gorillatag.kevsnametags", "Kevs Nametags", "1.0.8")]
    public class Plugin : BaseUnityPlugin
    {
        private float playerUpdateTimer;
        private float tagStatsTimer;
        private Camera mainCamera;
        private TMP_FontAsset utopium;

        private readonly Dictionary<VRRig, GameObject> nametags =
            new Dictionary<VRRig, GameObject>();

        private readonly Dictionary<VRRig, TextMeshPro> nametagTexts =
            new Dictionary<VRRig, TextMeshPro>();

        private readonly Dictionary<VRRig, Vector3> previousRigPositions =
            new Dictionary<VRRig, Vector3>();

        private readonly Dictionary<VRRig, float> previousRigPositionTimes =
            new Dictionary<VRRig, float>();

        private readonly Dictionary<VRRig, float> rigSpeeds =
            new Dictionary<VRRig, float>();

        private readonly HashSet<VRRig> currentRigs = new HashSet<VRRig>();
        private readonly List<VRRig> staleRigs = new List<VRRig>();

        private static readonly FieldInfo fpsField =
            typeof(VRRig).GetField("fps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo remoteVelocityField =
            typeof(VRRig).GetField("remoteVelocity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private const float FontRetryInterval = 5f;
        private float fontRetryTimer;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Awake()
        {
            FindFont();
        }

        private void Update()
        {
            playerUpdateTimer += Time.deltaTime;
            if (playerUpdateTimer >= 1f)
            {
                playerUpdateTimer -= 1f;
                UpdatePlayerList();
            }

            UpdateTagPositions();

            tagStatsTimer += Time.unscaledDeltaTime;
            if (tagStatsTimer >= 0.1f)
            {
                tagStatsTimer = 0f;
                UpdateTagStats();
            }
        }

        private void UpdatePlayerList()
        {
            if (utopium == null)
            {
                fontRetryTimer += 1f;
                if (fontRetryTimer >= FontRetryInterval)
                {
                    fontRetryTimer = 0f;
                    FindFont();
                }
            }

            VRRig[] rigs = Object.FindObjectsByType<VRRig>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

            currentRigs.Clear();
            currentRigs.UnionWith(rigs);
            staleRigs.Clear();

            foreach (var pair in nametags)
            {
                if (pair.Key == null || !currentRigs.Contains(pair.Key))
                    staleRigs.Add(pair.Key);
            }

            foreach (VRRig rig in staleRigs)
            {
                if (nametags.TryGetValue(rig, out GameObject tag))
                {
                    Destroy(tag);
                    nametags.Remove(rig);
                    nametagTexts.Remove(rig);
                    previousRigPositions.Remove(rig);
                    previousRigPositionTimes.Remove(rig);
                    rigSpeeds.Remove(rig);
                }
            }

            foreach (VRRig rig in rigs)
            {
                if (rig == null)
                    continue;

                if (rig.Creator == null)
                    continue;

                if (rig.Creator.IsLocal)
                    continue;

                if (!nametags.ContainsKey(rig))
                {
                    GameObject tag = new GameObject("Kev_Nametag");

                    TextMeshPro tmp = tag.AddComponent<TextMeshPro>();

                    tmp.fontSize = 6.2f;
                    tmp.lineSpacing = -10f;
                    tmp.alignment = TextAlignmentOptions.Center;

                    if (utopium != null)
                        tmp.font = utopium;

                    nametags.Add(rig, tag);
                    nametagTexts.Add(rig, tmp);
                }
            }
        }

        private void FindFont()
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset font in fonts)
            {
                if (font.name == "UtopiumPixel Raster")
                {
                    utopium = font;
                    break;
                }
            }
        }

        private string FormatNametag(VRRig rig)
        {
            int fps = 0;

            if (fpsField != null)
                fps = (int)fpsField.GetValue(rig);

            float speed = 0f;

            if (rigSpeeds.TryGetValue(rig, out float currentSpeed))
                speed = currentSpeed;

            int red = Mathf.Clamp(Mathf.RoundToInt(rig.playerColor.r * 9f), 0, 9);
            int green = Mathf.Clamp(Mathf.RoundToInt(rig.playerColor.g * 9f), 0, 9);
            int blue = Mathf.Clamp(Mathf.RoundToInt(rig.playerColor.b * 9f), 0, 9);

            return $"{rig.Creator?.NickName}\nFPS:{fps} Speed:{speed:0.00}m/s\nCOLOR:{red},{green},{blue}";
        }

        private Vector3 GetRigVelocity(VRRig rig)
        {
            Vector3 position = rig.transform.position;
            float currentTime = Time.time;
            Vector3 velocity = Vector3.zero;

            if (previousRigPositions.TryGetValue(rig, out Vector3 previousPosition) &&
                previousRigPositionTimes.TryGetValue(rig, out float previousTime))
            {
                float elapsed = currentTime - previousTime;

                if (elapsed > 0f)
                    velocity = (position - previousPosition) / elapsed;
            }

            previousRigPositions[rig] = position;
            previousRigPositionTimes[rig] = currentTime;

            if (velocity.sqrMagnitude == 0f && remoteVelocityField != null)
                velocity = (Vector3)remoteVelocityField.GetValue(rig);

            return velocity;
        }

        private void UpdateTagPositions()
        {
            foreach (var pair in nametags)
            {
                VRRig rig = pair.Key;
                GameObject tag = pair.Value;

                if (rig == null || tag == null)
                    continue;

                rigSpeeds[rig] = GetRigVelocity(rig).magnitude;

                tag.transform.position =
                    rig.headMesh.transform.position + Vector3.up * 0.60f;

                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                }

                if (mainCamera != null)
                {
                    tag.transform.LookAt(mainCamera.transform);
                    tag.transform.Rotate(0f, 180f, 0f);
                }

                float rigScale = Mathf.Clamp(rig.scaleFactor, 0.1f, 3f);
                Vector3 tagScale = Vector3.one * 0.25f * rigScale;
                if (tag.transform.localScale != tagScale)
                    tag.transform.localScale = tagScale;
            }
        }

        private void UpdateTagStats()
        {
            foreach (var pair in nametags)
            {
                VRRig rig = pair.Key;
                GameObject tag = pair.Value;

                if (rig == null || tag == null)
                    continue;

                if (!nametagTexts.TryGetValue(rig, out TextMeshPro text))
                    continue;

                if (utopium != null && text.font != utopium)
                    text.font = utopium;

                string newText = FormatNametag(rig);

                if (text.text != newText)
                    text.text = newText;

                if (text.color != rig.playerColor)
                    text.color = rig.playerColor;
            }
        }

        private void OnDestroy()
        {
            foreach (var pair in nametags)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }

            nametags.Clear();
            nametagTexts.Clear();
        }
    }
}