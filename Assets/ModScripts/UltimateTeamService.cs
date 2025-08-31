using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using static UnityEngine.Debug;
using System.Linq;

[RequireComponent(typeof(KMService))]
public class UltimateTeamService : MonoBehaviour
{
    public bool loaded = false;
    public bool connectedJson, connectedSprite = false;
    public Texture offlineSprite;
    public TextAsset offlineJson;
    public Texture spriteSheet;
    public List<KtaneModule> allMods;

    public static UltimateTeamService Instance { get; private set; }

    private bool isError;

    void Awake()
    {
        Instance = this;

        StartCoroutine(FetchRepo());
        StartCoroutine(FetchIconSprite());
    }

    IEnumerator FetchRepo()
    {
        try
        {
            Log("[Ultimate Team Service] Fetching repo data");

            using (var req = UnityWebRequest.Get("https://ktane.timwi.de/json/raw"))
            {
                yield return req.SendWebRequest();

                if (req.isHttpError || req.isNetworkError)
                {
                    isError = true;
                    Log("[Ultimate Team Service] Failed to connect to repo. Using JSON backup from 8/31/2025");
                }
                else
                    Log($"[Ultimate Team Service] Connected to repo successfully. Obtained {JsonConvert.DeserializeObject<Root>(req.downloadHandler.text).KtaneModules.Count(x => !new[] { "Widget", "Appendix" }.Contains(x.Type))}");

                allMods = JsonConvert.DeserializeObject<Root>(isError ? offlineJson.text : req.downloadHandler.text).KtaneModules.Where(x => !new[] { "Widget", "Appendix" }.Contains(x.Type)).ToList();

                if (isError)
                    yield break;
            }
        }
        finally
        {
            connectedJson = true;
        }
    }

    IEnumerator FetchIconSprite()
    {
        try
        {
            Log("[Ultimate Team Service] Fetching icon sprite");

            using (var req = UnityWebRequestTexture.GetTexture("https://ktane.timwi.de/iconsprite"))
            {
                yield return req.SendWebRequest();

                if (req.isHttpError || req.isNetworkError)
                {
                    isError = true;
                    Log("[Ultimate Team Service] Failed to fetch icon sprite. Using backup from 8/31/2025");
                }
                else
                    Log($"[Ultimate Team Service] Icon sprite fetch successful ({DownloadHandlerTexture.GetContent(req).width}x{DownloadHandlerTexture.GetContent(req).height})");

                spriteSheet = isError ? offlineSprite : DownloadHandlerTexture.GetContent(req);

                if (isError)
                    yield break;
            }
        }
        finally
        {
            connectedSprite = true;
        }
    }

    public void WaitForFetch(Action<bool> callback) => StartCoroutine(WaitForFetchRoutine(callback));

    IEnumerator WaitForFetchRoutine(Action<bool> callback)
    {
        yield return new WaitUntil(() => connectedJson && connectedSprite);
        callback.Invoke(isError);
    }
}