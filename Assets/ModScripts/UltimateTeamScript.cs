using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

using static UnityEngine.Debug;
using static UnityEngine.Random;

public class UltimateTeamScript : MonoBehaviour
{
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;
    public Image[] iconRender;
    public Image[] expertRender1, expertRender2, expertRender3, expertRender4, expertRender5, expertRender6;
    public Sprite timer, placeholder;
    public KMSelectable[] mainButtons;
    public KMSelectable flipBombButton;
    public KMSelectable[] expertCards;
    public KMSelectable statusLightButton;
    public Sprite[] arrowSprites;
    public Sprite[] expertBGSprites;
    public Image[] tickMarks;
    public Image bombCasing;
    public Image bombEdge;
    public Image throbber;
    public Image stamp;
    public MeshRenderer LED;
    public MeshRenderer surface;
    public Sprite[] profilePictures;
    public Image[] profilePictureRends;
    public Text[] expertNameRends;

    private KMAudio.KMAudioRef Sound;
    private Coroutine mainButtonsAnimCoroutine;
    private List<KtaneModule> allMods;
    private List<KtaneModule> virtualBomb = new List<KtaneModule>();
    private List<KtaneModule> realBomb = new List<KtaneModule>();
    private List<int[]> expertProficiencies = new List<int[]>();
    private List<List<int>> moduleProf = new List<List<int>>();
    private Texture spriteSheet;
    private Sprite[] icons = new Sprite[12];
    private int[] experts;
    private int[] expertIxsByScoreDesc;
    private string[] currExpertPrefDiffs = new string[6];
    private string[] currExpertNames = new string[6];
    private bool[] bossIx = new bool[12];
    private bool[] needyIx = new bool[12];
    private bool[] selected = new bool[6];
    private bool bombFlipped, cannotPress, rightMenu;

    private Image[][] renders;

    private static readonly string[] expertNames = { "1254", "AlexCorruptor", "Axo", "BigCrunch22", "Cinnabar", "Crazycaleb", "CyanixDash", "Danielstigman", "dicey", "Diffuse", "diskoQs", "Espik", "eXish", "Floofy Floofles", "GhostSalt", "GoodHood", "Gwen", "JyGein", "Kilo", "Konoko", "Kugel", "Kuro", "Lexa", "LilyFlair", "Lulu", "Mage", "Marksam", "MasQuéÉlite", "meh", "NShep", "Obvious", "Piissii", "Quinn Wuest", "redpenguin", "Rosenothorns03", "Scoping Landscape", "Setra", "Sierra", "tandyCake", "TheFullestCircle", "Timwi", "Varunaxx", "WitekWitek", "xorote", "Zaakeil", "Zaphod" };
    private static readonly string[] expertPreferredDiffs = { "Easy", "Easy", "Medium", "Hard", "Medium", "VeryHard/Extreme", "Easy", "VeryHard/Extreme", "Hard", "Medium", "Easy", "VeryHard/Extreme", "VeryHard/Extreme", "Easy", "Medium", "Easy", "Easy", "Easy", "Hard", "Medium", "Hard", "Easy", "Medium", "Easy", "Medium", "Medium", "VeryHard/Extreme", "Medium", "Hard", "Hard", "VeryHard/Extreme", "VeryHard/Extreme", "Hard", "Hard", "Medium", "VeryHard/Extreme", "Medium", "Easy", "Hard", "Easy", "Hard", "Hard", "Medium", "VeryHard/Extreme", "VeryHard/Extreme", "VeryHard/Extreme" };

    void Awake()
    {
        moduleId = moduleIdCounter++;

        renders = new Image[][] { expertRender1, expertRender2, expertRender3, expertRender4, expertRender5, expertRender6 };

        for (int i = 0; i < mainButtons.Length; i++)
        {
            int x = i;
            mainButtons[x].OnInteract += delegate { if (!cannotPress && (rightMenu ? x == 0 : x == 1)) mainButtonPress(x); return false; };
        }
        for (int i = 0; i < expertCards.Length; i++)
        {
            int x = i;
            expertCards[x].OnInteract += delegate { if (!cannotPress && rightMenu) cardPress(x); return false; };
            expertCards[x].OnHighlight += delegate { expertCards[x].GetComponent<Image>().sprite = expertBGSprites[1]; };
            expertCards[x].OnHighlightEnded += delegate { expertCards[x].GetComponent<Image>().sprite = expertBGSprites[0]; };
            tickMarks[x].transform.localScale = Vector3.zero;
        }
        mainButtons[0].transform.localPosition = new Vector3(mainButtons[0].transform.localPosition.x, 0.0078f, mainButtons[0].transform.localPosition.z);
        flipBombButton.OnInteract += delegate { if (!cannotPress && !rightMenu) flipBombButtonPress(); return false; };
        flipBombButton.OnHighlight += delegate { flipBombButton.GetComponent<Image>().sprite = arrowSprites[1]; };
        flipBombButton.OnHighlightEnded += delegate { flipBombButton.GetComponent<Image>().sprite = arrowSprites[0]; };
        statusLightButton.OnInteract += delegate
        {
            if (!cannotPress && rightMenu && !moduleSolved)
            {
                bool correct = true;
                for (int i = 0; i < 6; i++)
                    if (selected[i] != expertIxsByScoreDesc.Contains(i))
                        correct = false;

                if (correct)
                {
                    StopAllCoroutines();
                    LED.material.color = new Color32(34, 35, 38, 255);
                    StartCoroutine(solve());
                }
                else
                {
                    var selectedExperts = Enumerable.Range(0, 6).Where((name, ix) => selected[ix]).ToArray().Shuffle();
                    var strikeMessage = selectedExperts.Length > 0
                        ? $"{selectedExperts[0]} has been selected and decided to strike on {virtualBomb[expertProficiencies[selectedExperts[0]].PickRandom()].Name}, striking out the bomb entirely."
                        : "You haven’t selected anybody for this bomb, therefore blowing up the bomb as a result.";

                    Log($"[Ultimate Team #{moduleId}] {strikeMessage} Strike!");

                    Module.HandleStrike();
                }
            }
            return false;
        };
        bombEdge.transform.localScale = Vector3.zero;
        expertCards[0].transform.parent.localPosition = new Vector3(0.2f, 0, 0);
        bombCasing.transform.parent.localPosition = Vector3.zero;
        stamp.transform.localScale = Vector3.zero;
        StartCoroutine(throb());
    }

    void Start()
    {
        cannotPress = true;

        bombCasing.transform.parent.localScale = Vector3.zero;

        UltimateTeamService.Instance.WaitForFetch(onFetched);
    }

    void onFetched(bool error)
    {
        var instance = UltimateTeamService.Instance;

        if (error)
        {
            allMods = JsonConvert.DeserializeObject<Root>(instance.offlineJson.text).KtaneModules;
            spriteSheet = instance.offlineSprite;
        }
        else
        {
            allMods = instance.allMods;
            spriteSheet = instance.spriteSheet;
            StartCoroutine(LEDFlash());
        }

        cannotPress = false;
        bombCasing.transform.parent.localScale = Vector3.one;
        throbber.transform.parent.localScale = Vector3.zero;


        generateModule();
    }


    void mainButtonPress(int pos)
    {
        if (mainButtonsAnimCoroutine != null)
            StopCoroutine(mainButtonsAnimCoroutine);
        mainButtonsAnimCoroutine = StartCoroutine(mainButtonsAnim(pos));
        rightMenu = !rightMenu;
        Audio.PlaySoundAtTransform("main button", mainButtons[pos].transform);
        mainButtons[pos].AddInteractionPunch();
        StartCoroutine(switchMenu());
    }

    void flipBombButtonPress()
    {
        StartCoroutine(flipBombAnim());
        flipBombButton.AddInteractionPunch(0.5f);
    }

    void cardPress(int pos)
    {
        selected[pos] = !selected[pos];
        tickMarks[pos].transform.localScale = selected[pos] ? Vector3.one : Vector3.zero;
        if (Sound != null)
            Sound.StopSound();
        Sound = Audio.HandlePlaySoundAtTransformWithRef(selected[pos] ? "tick" : "erase", tickMarks[pos].transform, false);
    }

    void generateModule()
    {
        tryagain:

        var eligibleModules = allMods.ToList();

        // Step 1: pick 11 modules that will be on the virtual bomb
        virtualBomb.Clear();
        if (Range(0, 4) != 0)
        {
            var randomBoss = Enumerable.Range(0, eligibleModules.Count).Where(x => eligibleModules[x].BossStatus == "FullBoss" || eligibleModules[x].BossStatus == "SemiBoss" && (eligibleModules[x].X != 0 || eligibleModules[x].Y != 0)).PickRandom();

            virtualBomb.Add(eligibleModules[randomBoss]);

            eligibleModules.RemoveAt(randomBoss);
        }

        if (Range(0, 2) != 0)
        {
            var randomNeedy = Enumerable.Range(0, eligibleModules.Count).Where(x => eligibleModules[x].Type == "Needy" && (eligibleModules[x].X != 0 || eligibleModules[x].Y != 0)).PickRandom();

            virtualBomb.Add(eligibleModules[randomNeedy]);

            eligibleModules.RemoveAt(randomNeedy);
        }


        var eligibleRemainingModules = eligibleModules.Where(m => m.Type == "Regular" && m.BossStatus != "FullBoss" && m.BossStatus != "SemiBoss" && (m.X != 0 || m.Y != 0)).ToList();
        while (virtualBomb.Count < 11)
        {
            var ix = Range(0, eligibleRemainingModules.Count);
            virtualBomb.Add(eligibleRemainingModules[ix]);
            eligibleRemainingModules.RemoveAt(ix);
        }
        virtualBomb.Add(null);
        virtualBomb.Shuffle();

        // Step 2: generate all possible sets of expert proficiencies (1–3 modules per expert)
        var eligibilitySets = Enumerable.Range(0, 3).Select(_ => new List<int[]>()).ToArray();
        for (var val = 0; val < (1 << 11); val++)
        {
            var list = new List<int>();
            for (var b = 0; b < 11; b++)
                if ((val & (1 << b)) != 0)
                    list.Add(b);
            if (list.Count >= 1 && list.Count <= 3 && list.All(mIx => virtualBomb[mIx] != null && virtualBomb[mIx].ExpertDifficulty != "Trivial" && virtualBomb[mIx].ExpertDifficulty != "VeryEasy"))
                eligibilitySets[list.Count - 1].Add(list.ToArray());
        }

        // Step 3: decide on the expert proficiencies
        expertProficiencies.Clear();
        for (var expIx = 0; expIx < 6; expIx++)
        {
            var proficiencySize = new[] { 1, 2, 2, 2, 3 }.PickRandom();
            if (eligibilitySets[proficiencySize - 1].Count == 0)
                goto tryagain;
            var elIx = Range(0, eligibilitySets[proficiencySize - 1].Count);
            expertProficiencies.Add(eligibilitySets[proficiencySize - 1][elIx]);
            eligibilitySets[proficiencySize - 1].RemoveAt(elIx);
        }

        // Step 4: determine the experts proficient in each module, and make sure that each module has at most 2 experts
        moduleProf.Clear();
        for (int modIx = 0; modIx < 12; modIx++)
        {
            var listofExperts = new List<int>();
            for (int expIx = 0; expIx < 6; expIx++)
                if (expertProficiencies[expIx].Contains(modIx))
                    listofExperts.Add(expIx);
            if (listofExperts.Count > 2)
                goto tryagain;
            moduleProf.Add(listofExperts);
        }

        // Generate sprites
        var maxY = allMods.Max(mod => mod.Y);

        for (var modIx = 0; modIx < virtualBomb.Count; modIx++)
        {
            needyIx[modIx] = virtualBomb[modIx] != null && virtualBomb[modIx].Type == "Needy";
            bossIx[modIx] = virtualBomb[modIx] != null && (virtualBomb[modIx].BossStatus == "FullBoss" || virtualBomb[modIx].BossStatus == "SemiBoss");
            icons[modIx] = virtualBomb[modIx] == null ? timer : createSprite(virtualBomb[modIx], maxY);
        }

        foreach (string modID in Bomb.GetModuleIDs())
            realBomb.AddRange(allMods.Where(m => m.ModuleID == modID));




        experts = Enumerable.Range(0, profilePictures.Length).ToArray().Shuffle();

        for (int i = 0; i < 6; i++)
        {
            currExpertNames[i] = expertNames[experts[i]];
            currExpertPrefDiffs[i] = expertPreferredDiffs[experts[i]];
        }

        Log($"[Ultimate Team #{moduleId}] The virtual bomb is displayed as follows:");
        for (var modIx = 0; modIx < virtualBomb.Count; modIx++)
            Log($@"[Ultimate Team #{moduleId}] {(virtualBomb[modIx] == null ? "[TIMER]" :
                $"{virtualBomb[modIx].Name} — {virtualBomb[modIx].ExpertDifficulty} — {(moduleProf[modIx].Count == 0 ? "(none)" : moduleProf[modIx].Select(expIx => currExpertNames[expIx]).Join(", "))}")}");

        Log($"[Ultimate Team #{moduleId}] The candidates of experts are: {currExpertNames.Join(", ")}");
        Log($"[Ultimate Team #{moduleId}] These experts have these preferred difficulties: {currExpertPrefDiffs.Join(", ").Replace("VeryEasy", "Very Easy").Replace("VeryHard/Extreme", "Very Hard / Extreme")}");
        Log($"[Ultimate Team #{moduleId}] The proficiencies are as follows: {moduleProf.Select((expIxs, modIx) => virtualBomb[modIx] == null ? "[TIMER]" + " — " + "none" : virtualBomb[modIx].Name + " — " + (expIxs.Count == 0 ? "none" : expIxs.Select(y => currExpertNames[y]).Join(", "))).Join("; ")}.");

        displaySprites();
        calculations();
    }

    private void displaySprites()
    {
        for (int i = 0; i < 6; i++)
        {
            iconRender[i].sprite = icons[bombFlipped ? i + 6 : i];
            profilePictureRends[i].sprite = profilePictures[experts[i]];
            expertNameRends[i].text = currExpertNames[i].ToUpperInvariant();

            for (int j = 0; j < 2; j++)
            {
                renders[i][j].sprite = placeholder;
                renders[i][j].enabled = virtualBomb[bombFlipped ? i + 6 : i] != null;
            }

            for (int j = 0; j < moduleProf[bombFlipped ? i + 6 : i].Count; j++)
                renders[i][j].sprite = profilePictures[experts[moduleProf[bombFlipped ? i + 6 : i][j]]];
        }
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 2; j++)
                if (renders[i][j].sprite.name == "!placeholder")
                    renders[i][j].enabled = false;
    }

    private Sprite createSprite(KtaneModule module, int maxY)
    {
        var sprite = Sprite.Create(spriteSheet as Texture2D, new Rect(32 * module.X, 32 * (maxY - module.Y), 32, 32), new Vector2(0.5f, 0.5f));
        sprite.texture.filterMode = FilterMode.Point;
        return sprite;
    }

    void calculations()
    {
        var scores = ScoringSystem.baseScores(Bomb.GetSerialNumber());
        Log($"[Ultimate Team #{moduleId}] The assigned scores for the experts in reading order: {scores.Join(", ")}");

        var ixes = new[] { "Easy", "Medium", "Hard", "VeryHard" };

        for (var expIx = 0; expIx < 6; expIx++)
            foreach (var modIx in expertProficiencies[expIx])
                scores[expIx] += virtualBomb[modIx].ExpertDifficulty == "Extreme" ? 4 : Array.IndexOf(ixes, virtualBomb[modIx].ExpertDifficulty) + 1;

        Log($"[Ultimate Team #{moduleId}] After adding proficiency scores for each expert in reading order: {scores.Join(", ")}");
        for (var expIx = 0; expIx < 6; expIx++)
            for (var modIx = 0; modIx < virtualBomb.Count; modIx++)
                if (virtualBomb[modIx] != null && (
                    currExpertPrefDiffs[expIx] == virtualBomb[modIx].ExpertDifficulty ||
                    (currExpertPrefDiffs[expIx] == "VeryHard/Extreme" && (virtualBomb[modIx].ExpertDifficulty == "VeryHard" || virtualBomb[modIx].ExpertDifficulty == "Extreme"))))
                    scores[expIx]++;

        Log($"[Ultimate Team #{moduleId}] After adding preferred difficulty scores: {scores.Join(", ")}");
        scores = ScoringSystem.modifyingScores(Bomb, virtualBomb, realBomb, scores, experts.ToArray());
        Log($"[Ultimate Team #{moduleId}] Final scores: {scores.Join(", ")}");

        expertIxsByScoreDesc = Enumerable.Range(0, 6).OrderByDescending(ix => scores[ix]).ToArray();
        var numEasy = virtualBomb.Count(m => m != null && (m.ExpertDifficulty == "Trivial" || m.ExpertDifficulty == "VeryEasy"));
        var hasUnicornScore = scores.Any(score => score == 66669420);
        if (numEasy >= 4 || hasUnicornScore)
        {
            expertIxsByScoreDesc = expertIxsByScoreDesc.Take(1).ToArray();
            Log($@"[Ultimate Team #{moduleId}] {(hasUnicornScore
                ? "CyanixDash is your only expert since the firstmost condition applied. Don’t take anyone else."
                : "Since there are at least 4 Very Easy/Trivial modules on the virtual bomb, you’ll be taking only one expert.")}");
        }
        else
        {
            expertIxsByScoreDesc = expertIxsByScoreDesc.Take(2).ToArray();
            Log($"[Ultimate Team #{moduleId}] Since there are less than 4 Very Easy/Trivial modules on the virtual bomb, you’ll be taking two experts.");
        }
        Log($@"[Ultimate Team #{moduleId}] {(expertIxsByScoreDesc.Length == 1
            ? $"According to the highest score, you should take {currExpertNames[expertIxsByScoreDesc[0]]}."
            : $"According to the two highest scores, you should take {currExpertNames[expertIxsByScoreDesc[0]]} and {currExpertNames[expertIxsByScoreDesc[1]]}.")}");
    }

    private IEnumerator solve()
    {
        Log($"[Ultimate Team #{moduleId}] You have selected the correct expert(s). Solved!");
        moduleSolved = true;
        Module.HandlePass();
        stamp.transform.localScale = Vector3.one;
        expertCards[3].AddInteractionPunch(5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Stamp, stamp.transform);
        expertCards[0].transform.parent.localScale = Vector3.zero;
        bombCasing.transform.parent.localScale = Vector3.zero;
        cannotPress = true;
        float timer = 0;
        while (timer < 0.5f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Audio.PlaySoundAtTransform("solve", Module.transform);
    }

    private IEnumerator mainButtonsAnim(int pos, float duration = 0.05f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            mainButtons[pos].transform.localPosition = Vector3.Lerp(new Vector3(mainButtons[pos].transform.localPosition.x, 0.01441f, mainButtons[pos].transform.localPosition.z), new Vector3(mainButtons[pos].transform.localPosition.x, 0.0078f, mainButtons[pos].transform.localPosition.z), timer / duration);
            mainButtons[1 - pos].transform.localPosition = Vector3.Lerp(new Vector3(mainButtons[1 - pos].transform.localPosition.x, 0.0078f, mainButtons[1 - pos].transform.localPosition.z), new Vector3(mainButtons[1 - pos].transform.localPosition.x, 0.01441f, mainButtons[1 - pos].transform.localPosition.z), timer / duration);
        }
        mainButtons[pos].transform.localPosition = new Vector3(mainButtons[pos].transform.localPosition.x, 0.0078f, mainButtons[pos].transform.localPosition.z);
        mainButtons[1 - pos].transform.localPosition = new Vector3(mainButtons[1 - pos].transform.localPosition.x, 0.01441f, mainButtons[1 - pos].transform.localPosition.z);
    }

    private IEnumerator flipBombAnim(float duration = 0.2f)
    {
        cannotPress = true;
        Audio.PlaySoundAtTransform("spin", flipBombButton.transform);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            bombCasing.transform.localScale = new Vector3(0.9f * Mathf.Cos(0.5f * Mathf.PI * timer / duration), 0.9f, 0.9f);
            bombEdge.transform.localScale = new Vector3(0.9f * Mathf.Sin(0.5f * Mathf.PI * timer / duration), 0.9f, 0.9f);
            bombCasing.transform.localPosition = new Vector3(Easing.OutSine(timer, 0, 0.0135f, duration), bombCasing.transform.localPosition.y, 0);
            bombEdge.transform.localPosition = new Vector3(Easing.InSine(timer, -0.06975f, 0, duration), bombEdge.transform.localPosition.y, 0);
        }
        bombFlipped = !bombFlipped;
        displaySprites();
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            bombCasing.transform.localScale = new Vector3(0.9f * Mathf.Sin(0.5f * Mathf.PI * timer / duration), 0.9f, 0.9f);
            bombEdge.transform.localScale = new Vector3(0.9f * Mathf.Cos(0.5f * Mathf.PI * timer / duration), 0.9f, 0.9f);
            bombCasing.transform.localPosition = new Vector3(Easing.InSine(timer, -0.0135f, 0, duration), bombCasing.transform.localPosition.y, 0);
            bombEdge.transform.localPosition = new Vector3(Easing.OutSine(timer, 0, 0.06975f, duration), bombEdge.transform.localPosition.y, 0);
        }
        bombCasing.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        bombCasing.transform.localPosition = new Vector3(0, bombCasing.transform.localPosition.y, 0);
        bombEdge.transform.localScale = Vector3.zero;
        bombEdge.transform.localPosition = new Vector3(0.06975f, bombEdge.transform.localPosition.y, 0);
        cannotPress = false;
    }

    private IEnumerator switchMenu(float duration = 0.15f)
    {
        cannotPress = true;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            expertCards[0].transform.parent.localPosition = Vector3.Lerp(new Vector3(!rightMenu ? 0 : 0.2f, 0, 0), new Vector3(!rightMenu ? 0.2f : 0, 0, 0), timer / duration);
            bombCasing.transform.parent.localPosition = Vector3.Lerp(new Vector3(!rightMenu ? -0.2f : 0, 0, 0), new Vector3(!rightMenu ? 0 : -0.2f, 0, 0), timer / duration);
            surface.material.SetTextureOffset("_MainTex", new Vector2(Mathf.Lerp(rightMenu ? 0 : 0.1f, rightMenu ? 0.1f : 0, timer / duration), 0));
        }
        expertCards[0].transform.parent.localPosition = new Vector3(!rightMenu ? 0.2f : 0, 0, 0);
        bombCasing.transform.parent.localPosition = new Vector3(!rightMenu ? 0 : -0.2f, 0, 0);
        surface.material.SetTextureOffset("_MainTex", new Vector2(rightMenu ? 0.1f : 0, 0));
        cannotPress = false;
    }

    private IEnumerator LEDFlash(float interval = 0.5f)
    {
        int i = 0;
        while (true)
        {
            LED.material.color = new[] { new Color32(34, 35, 38, 255), new Color32(186, 68, 35, 255) }[i];
            i = 1 - i;
            float timer = 0;
            while (timer < interval)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator throb(float interval = 0.05f)
    {
        int i = 0;
        while (true)
        {
            throbber.transform.localEulerAngles = throbber.transform.localEulerAngles - new Vector3(0, 0, 30);
            i = 1 - i;
            float timer = 0;
            while (timer < interval)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use '!{0} switch' to change menus. Use '!{0} flip' to flip the virtual bomb on the Mission menu, and use '!{0} submit 1 3' to select the first and third experts in reading order in the Experts menu (and deselect any others), then press the status light.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        string[] commandArray = command.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        if (commandArray.Length == 0)
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
        if (command == "switch")
        {
            yield return null;
            mainButtons[rightMenu ? 0 : 1].OnInteract();
        }
        else if (command == "flip" && !rightMenu)
        {
            yield return null;
            flipBombButton.OnInteract();
        }
        else if (command == "flip")
        {
            yield return "sendtochaterror You can't use that command in the Experts menu!";
            yield break;
        }
        else if (commandArray.First() == "submit" && rightMenu)
        {
            if (commandArray.Length > 1 && commandArray.Length < 4)
            {
                for (int i = 0; i < commandArray.Length - 1; i++)
                {
                    int thing = 0;
                    if (!int.TryParse(commandArray[i + 1], out thing) || int.Parse(commandArray[i + 1]) < 1 || int.Parse(commandArray[i + 1]) > 6)
                    {
                        yield return "sendtochaterror I can't select " + (commandArray.Length == 2 ? "that expert!" : "those experts!");
                        yield break;
                    }
                }
                List<int> chosen = new List<int>();
                for (int i = 0; i < commandArray.Length - 1; i++)
                    chosen.Add(int.Parse(commandArray[i + 1]) - 1);
                yield return null;
                for (int i = 0; i < 6; i++)
                    if (chosen.Contains(i) != selected[i])
                    {
                        expertCards[i].OnInteract();
                        float timer = 0;
                        while (timer < 0.1f)
                        {
                            yield return null;
                            timer += Time.deltaTime;
                        }
                    }
                statusLightButton.OnInteract();
            }
            else
            {
                yield return "sendtochaterror I can't select those experts!";
                yield break;
            }
        }
        else if (commandArray.First() == "submit")
        {
            yield return "sendtochaterror You can't use that command in the Mission menu!";
            yield break;
        }
        else
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (cannotPress)
            yield return true;
        if (!rightMenu)
            mainButtons[1].OnInteract();
        while (cannotPress)
            yield return true;
        for (int i = 0; i < 6; i++)
            if (selected[i] != expertIxsByScoreDesc.Contains(i))
                expertCards[i].OnInteract();
        yield return true;
        statusLightButton.OnInteract();
    }
}