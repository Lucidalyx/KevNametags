using BepInEx;
using Photon.Pun;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KevsNametags
{
    [BepInPlugin("org.kevsmod.gorillatag.kevsnametags", "Kevs Nametags", "1.0.7")]
    public class Plugin : BaseUnityPlugin
    {
        private float playerUpdateTimer;
        private Camera mainCamera;
        private TMP_FontAsset utopium;

        private readonly Dictionary<VRRig, GameObject> nametags =
            new Dictionary<VRRig, GameObject>();

        private static readonly FieldInfo fpsField =
            typeof(VRRig).GetField("fps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Awake()
        {
            utopium = Resources
                .FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(f => f.name == "UtopiumPixel Raster");
        }

        private void Update()
        {
            playerUpdateTimer += Time.deltaTime;

            // Expensive stuff
            if (playerUpdateTimer >= 1f)
            {
                playerUpdateTimer = 0f;
                UpdatePlayerList();
            }

            // Smooth stuff
            UpdateTagPositions();
        }

        private void UpdatePlayerList()
        {
            if (utopium == null)
            {
                utopium = Resources
                    .FindObjectsOfTypeAll<TMP_FontAsset>()
                    .FirstOrDefault(f => f.name == "UtopiumPixel Raster");
            }

            VRRig[] rigs = Object.FindObjectsByType<VRRig>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

            HashSet<VRRig> currentRigs = new HashSet<VRRig>(rigs);
            List<VRRig> staleRigs = new List<VRRig>();

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

                    tmp.text = FormatNametag(rig);
                    tmp.color = rig.playerColor;

                    nametags.Add(rig, tag);
                }

                TextMeshPro text = nametags[rig].GetComponent<TextMeshPro>();

                if (utopium != null && text.font != utopium)
                    text.font = utopium;

                string newName = FormatNametag(rig);

                if (text.text != newName)
                    text.text = newName;

                if (text.color != rig.playerColor)
                    text.color = rig.playerColor;
            }
        }

        private string FormatNametag(VRRig rig)
        {
            int fps = 0;

            if (fpsField != null)
                fps = (int)fpsField.GetValue(rig);

            return $"{rig.Creator?.NickName}\nFPS: {fps}\nPing: {PhotonNetwork.GetPing()} ms";
        }

        private void UpdateTagPositions()
        {
            foreach (var pair in nametags)
            {
                VRRig rig = pair.Key;
                GameObject tag = pair.Value;

                if (rig == null || tag == null)
                    continue;

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
                tag.transform.localScale = Vector3.one * 0.25f * rigScale;
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
        }
    }
}