using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class simonStumblesScript : MonoBehaviour {

	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;
	public KMColorblindMode Blind;

	public enum Direction
    {
		Up,
		Left, 
		Right, 
		Down
    }
	public static readonly Direction[] baseDirections = new Direction[] { Direction.Up, Direction.Left, Direction.Right, Direction.Down };

	public enum ColorType
    {
		Red, 
		Yellow, 
		Green, 
		Blue, 
		White
    }
	public static readonly ColorType[] baseColors = new ColorType[4] { ColorType.Red, ColorType.Yellow, ColorType.Green, ColorType.Blue };

	public Dictionary<ColorType, Color> ColorValues = new Dictionary<ColorType, Color>
	{
		{ ColorType.Red, new Color(1f, 0.066f, 0.066f, 0.8f) },
		{ ColorType.Yellow, new Color(1f, 1f, 0.034f, 0.8f) },
		{ ColorType.Green, new Color(0.066f, 1f, 0.189f, 0.8f) },
		{ ColorType.Blue, new Color(0f, 0.752f, 1f, 0.8f) },
		{ ColorType.White, new Color(1f, 1f, 1f, 1f)}
	};

    public Direction[][] table = new Direction[4][]
    {
        new Direction[4] { Direction.Down, Direction.Up, Direction.Right, Direction.Left },
        new Direction[4] { Direction.Right, Direction.Left, Direction.Up, Direction.Down },
        new Direction[4] { Direction.Up, Direction.Down, Direction.Left, Direction.Right },
        new Direction[4] { Direction.Left, Direction.Right, Direction.Down, Direction.Up }
    };

    public KMSelectable[] KeypadButtons; // Up, Left, Right, Down, Center (stumble)
	public AudioClip[] SoundEffects;
	public GameObject[] ButtonMats;
	public Material[] ColorMats;
	public Light[] ButtonLights;
	public TextMesh[] CBTexts;

	private ColorType[] previousColors = new ColorType[4] { ColorType.Red, ColorType.Yellow, ColorType.Green, ColorType.Blue };
	private ColorType[] startColors = new ColorType[4] { ColorType.Red, ColorType.Yellow, ColorType.Green, ColorType.Blue };
	private bool animating = true;
	private bool moduleSolved = false;
	private Coroutine[] FlashWaitRoutines = new Coroutine[2];
	private int stage = 1;
	private int pressProgress = 0;
	private bool preStage = true;
	private ColorType[] Flashing = new ColorType[5];
	private Direction[] Sounds = new Direction[5];
	private ColorType[] ColorPresses = new ColorType[5] { ColorType.White, ColorType.White, ColorType.White, ColorType.White, ColorType.White };
	private bool stumbleReqd = false;
	private string[] numberWords = new string[5] { "first", "second", "third", "fourth", "fifth" };
	private bool tpCB = false;

	private static int moduleIdCounter = 1;
	private int moduleID;

    // Use this for initialization
    void Start () {
		moduleID = moduleIdCounter++;
		foreach (KMSelectable Button in KeypadButtons)
        {
			Button.OnInteract += delegate () { ButtonPress(Button); return false; };
        }
		previousColors.Shuffle();
		for (int i = 0; i < 4; i++)
        {
			ButtonMats[i].GetComponent<MeshRenderer>().material.color = ColorValues[previousColors[i]];
			ButtonLights[i].color = ColorValues[previousColors[i]];
			startColors[i] = previousColors[i];
			ButtonLights[i].enabled = false;
			ButtonLights[i].range = transform.lossyScale.x * 0.1f;
			ButtonLights[i].intensity = 10f;

         if (Blind.ColorblindModeActive || tpCB)
				CBTexts[i].text = previousColors[i].ToString();
		}
		animating = false;

		for (int i = 0; i < 5; i++)
        {
			Flashing[i] = baseColors[Rnd.Range(0, 4)];
			Sounds[i] = baseDirections[Rnd.Range(0, 4)];
		}
		Log("The initial colors are " + startColors[0].ToString() + ", " + startColors[1].ToString() + ", " + startColors[2].ToString() + ", and " + startColors[3].ToString() + ".");
		Log("The order of flashing colors is " + Flashing[0].ToString() + ", " + Flashing[1].ToString() + ", " + Flashing[2].ToString() + ", " + Flashing[3].ToString() + ", and " + Flashing[4].ToString() + ".");
		Log("The directions stated are " + Sounds[0].ToString() + ", " + Sounds[1].ToString() + ", " + Sounds[2].ToString() + ", " + Sounds[3].ToString() + ", and " + Sounds[4].ToString() + ".");
		Log("For more specific information, please see the Filtered Log. Search for <Simon Stumbles #" + moduleID + ">.");
	}
	
	// Update is called once per frame
	void Update () {

	}

	void Log(string message)
    {
		Debug.Log("[Simon Stumbles #" + moduleID + "] " + message);
    }

	void DebugLog(string message)
	{
		Debug.Log("<Simon Stumbles #" + moduleID + "> " + message);
	}

	public void ButtonPress(KMSelectable Button)
	{
		bool toStumble = false;
		bool unicorn = false;
		int index = Array.IndexOf(KeypadButtons, Button);
		if (animating)
			return;
		if (FlashWaitRoutines[0] != null)
		{
			StopCoroutine(FlashWaitRoutines[0]);
			for (int i = 0; i < 4; i++)
				ButtonLights[i].enabled = false;
		}
		if (FlashWaitRoutines[1] != null)
		{
			StopCoroutine(FlashWaitRoutines[1]);
			for (int i = 0; i < 4; i++)
				ButtonLights[i].enabled = false;
		}
        DebugLog("Stage #" + stage.ToString() + ", Flash #" + (pressProgress + 1).ToString() + " | Pressed " + EButton(index, false) + " | Expected " + cPress());
		if (index == 4)
		{
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			Button.AddInteractionPunch();
			Audio.PlaySoundAtTransform(SoundEffects[4].name, transform);
			if (moduleSolved)
				return;
			if (preStage)
			{
				Log("Module initiated. Let's see how this goes, shall we?");
				StartCoroutine(Stumble(null));
				preStage = false;
				FlashWaitRoutines[0] = StartCoroutine(FlashColors());
				return;
			}
			else if (stumbleReqd)
			{
				//DebugLog("Stumble pressed, and you needed to. Nice!");
				toStumble = true;
				stumbleReqd = false;
                pressProgress++;
            }
			else
			{
				//Log("Stumble pressed, but you didn't need to. Strike!");
				Strike(index);
				return;
			}
		}
		else
		{
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			Button.AddInteractionPunch();
			FlashWaitRoutines[1] = StartCoroutine(FlashButton(index));
			if (preStage || moduleSolved)
				return;
			//it's a button, so we need to check the press. 
			if (stumbleReqd)
			{
				//Log("I expected you to press stumble, but you pressed " + EButton(index, false) + ". Strike!");
				Strike(index);
				return;
			}
			//DebugLog("Stage: " + stage.ToString() + " | pressProgress (0 index): " + pressProgress.ToString() +  " | Direction Pressed: " + baseDirections[index].ToString() + " | Color Pressed: " + previousColors[index].ToString() + " | Table Lookup: " + table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])].ToString() + " | Assoc. Press Class: " + ColorPresses[pressProgress] + "."); ;
			if (previousColors[0] == startColors[0] && previousColors[1] == startColors[1] && previousColors[2] == startColors[2])
			{
				//DebugLog("Currently displayed colors are what was initially shown, using the 'However' condition.");
				unicorn = true;
				/*if (Flashing[pressProgress] == previousColors[index])
					DebugLog("You pressed the flashing color (" + Flashing[pressProgress].ToString() + "), correct!");
				else
				{
					Log("You did not press the flashing color, Strike!");
					Strike(index);
					return;
				}*/
                if (Flashing[pressProgress] != previousColors[index])
                {
                    Strike(index);
                    return;
                }
			}
			/*else if (baseDirections[index] == table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])] && ColorPresses[pressProgress] == ColorType.White)
				DebugLog("The previous stage's " + numberWords[pressProgress] + " press required a stumble or did not exist, table used correctly.");
			else if (ColorPresses[pressProgress] == previousColors[index])
				DebugLog("The previous stage's " + numberWords[pressProgress] + " press did not require a stumble, so you pressed the same color as was last pressed, correct.");*/
			else if (!(baseDirections[index] == table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])] && ColorPresses[pressProgress] == ColorType.White) && !(ColorPresses[pressProgress] == previousColors[index]))
			{
				/*if (ColorPresses[pressProgress] == ColorType.White)
					Log("With the last press irrelevant, you needed to press " + table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])].ToString() + " according to the table, but you pressed " + baseDirections[index].ToString() + ". Strike!");
				else
					Log("The press for this step was " + ColorPresses[pressProgress].ToString() + " due to the same color in a previous stage, but you pressed " + previousColors[index] + ". Strike!");*/
				Strike(index);
				return;
			}
			if ((previousColors[index] == Flashing[pressProgress] || Array.IndexOf(baseDirections, Sounds[pressProgress]) == index) && !unicorn)
			{
				/*if (previousColors[index] == Flashing[pressProgress])
					DebugLog("The flashing color is the same as the color you pressed, so you'll need to stumble.");
				if (Array.IndexOf(baseDirections, Sounds[pressProgress]) == index)
					DebugLog("The stated direction is the same as the direction you pressed, so you'll need to stumble.");*/
				stumbleReqd = true;
				ColorPresses[pressProgress] = ColorType.White;
			}
            else
            {
                ColorPresses[pressProgress] = previousColors[index];
                pressProgress++;
            }
			if (unicorn && pressProgress == stage)
            {
				//DebugLog("All colors pressed for 'However' condition, so you'll need to stumble.");
				stumbleReqd = true;
			}
		}
		if (pressProgress == stage && !stumbleReqd)
        {
			stage++;
			pressProgress = 0;
			//Log("Stage " + (stage - 1).ToString() + " completed, proceeding...");
			if (stage > 5)
            {
				Log("All inputs entered, module solved.");
				Module.HandlePass();
				moduleSolved = true;
				StartCoroutine(Stumble(new ColorType[4] { ColorType.White, ColorType.White, ColorType.White, ColorType.White }));
				return;
            }
			else
				FlashWaitRoutines[0] = StartCoroutine(FlashColors());
		}
		if (toStumble)
			StartCoroutine(Stumble(null));
    }
	void Strike(int press)
    {
        Log("Stage #" +  stage.ToString() + ", Flash #" + (pressProgress + 1).ToString() + " | I expected you to press " + cPress() + ", but you pressed " + EButton(press, false) + ". Strike!");
		//for (int i = 0; i < 4; i++)
			//ButtonLights[i].enabled = false;
		StartCoroutine(Stumble(startColors));
		for (int i = 0; i < 5; i++)
			ColorPresses[i] = ColorType.White;
		preStage = true;
		stumbleReqd = false;
		pressProgress = 0;
		Module.HandleStrike();
	}

    string EButton(int pos, bool start)
    {
        if (pos == 4)
            return "Stumble";
        if (start)
            return startColors[pos].ToString() + " / " + baseDirections[pos].ToString();
        else
            return previousColors[pos].ToString() + " / " + baseDirections[pos].ToString();
    }

    string cPress()
    {
        if (stumbleReqd || preStage)
            return "Stumble due to " + (preStage ? "the start of the module" : (previousColors[0] == startColors[0] && previousColors[1] == startColors[1] && previousColors[2] == startColors[2]) ? "the 'However' condition" : "the previous button press");
        else if (previousColors[0] == startColors[0] && previousColors[1] == startColors[1] && previousColors[2] == startColors[2])
            return EButton(Array.IndexOf(previousColors, Flashing[pressProgress]), false) + " due to the 'However' condition";
        else if (ColorPresses[pressProgress] != ColorType.White)
            return EButton(Array.IndexOf(previousColors, ColorPresses[pressProgress]), false) + " due to a previously valid press";
        else
            return EButton(Array.IndexOf(baseDirections, table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])]), false) + " due to no previously valid press";
    }

	IEnumerator Stumble(ColorType[] toSet)
    {
		if (toSet == null)
			toSet = new ColorType[] { baseColors[0], baseColors[1], baseColors[2], baseColors[3] }.Shuffle();
		animating = true;
		float amountChanged = 0f;
		for (int i = 0; i < 4; i++)
			CBTexts[i].text = "";
		while (amountChanged < 1)
		{
			amountChanged += Time.deltaTime * 0.75f;
			for (int i = 0; i < 4; i++)
            {
				ButtonLights[i].color = Color.Lerp(ColorValues[previousColors[i]], ColorValues[toSet[i]], amountChanged);
				ButtonMats[i].GetComponent<MeshRenderer>().material.color = Color.Lerp(ColorValues[previousColors[i]], ColorValues[toSet[i]], amountChanged);
			}
			yield return null;
		}
		for (int i = 0; i < 4; i++)
        {
			previousColors[i] = toSet[i];
			if ((Blind.ColorblindModeActive || tpCB) && previousColors[i] != ColorType.White)
				CBTexts[i].text = toSet[i].ToString();
        }
		animating = false;
		DebugLog("Stumbled, displaying " + previousColors[0].ToString() + ", " + previousColors[1].ToString() + ", " + previousColors[2].ToString() + ", and " + previousColors[3].ToString() + ".");
		yield return null;
    }

	IEnumerator FlashColors()
    {
		yield return new WaitForSeconds(3.0f);
		while (true)
        {
			while (animating)
				yield return new WaitForSeconds(0.01f);
			for (int i = 0; i < stage; i++)
            {
				//ButtonLights[Array.IndexOf(previousColors, Flashing[i])].color = ColorValues[Flashing[i]];
				ButtonLights[Array.IndexOf(previousColors, Flashing[i])].enabled = true;
				Audio.PlaySoundAtTransform(SoundEffects[Array.IndexOf(baseDirections, Sounds[i])].name, transform);
				yield return new WaitForSeconds(1f);
				ButtonLights[Array.IndexOf(previousColors, Flashing[i])].enabled = false;
				yield return new WaitForSeconds(0.5f);
            }
			yield return new WaitForSeconds(2f);
        }
    }

	IEnumerator FlashButton(int index)
    {
		ButtonLights[index].enabled = true;
		Audio.PlaySoundAtTransform(SoundEffects[index].name, transform);
		yield return new WaitForSeconds(0.5f);
		ButtonLights[index].enabled = false;
		yield break;
    }

	public readonly string TwitchHelpMessage = "Press the buttons using !{0} <button>. Valid buttons are (R)ed, (Y)ellow, (G)reen, (B)lue, and (S)tumble. Toggle colorblind mode with !{0} (c)olorblind. Buttons are chainable with spaces.";
	IEnumerator ProcessTwitchCommand(string command)
	{
		while (animating) yield return new WaitForSeconds(0.01f);
		string[] parameters = command.ToUpperInvariant().Trim().Split(' ').ToArray();
		if (parameters.Count() < 1 || parameters.Count() > 6)
			yield break;
		if (parameters.Count() == 1 && (parameters[0] == "COLORBLIND" || parameters[0] == "C"))
        {
			yield return null;
			if (tpCB)
            {
				tpCB = false;
				for (int i = 0; i < 4; i++)
					CBTexts[i].text = "";
			}
			else
            {
				tpCB = true;
				for (int i = 0; i < 4; i++)
					CBTexts[i].text = previousColors[i].ToString();
			}
			yield break;
        }
		bool[] valid = new bool[parameters.Count()];
		for (int i = 0; i < parameters.Count(); i++)
		{
			valid[i] = (parameters[i] == "STUMBLE" || parameters[i] == "RED" || parameters[i] == "YELLOW" || parameters[i] == "GREEN" || parameters[i] == "BLUE" || parameters[i] == "S" || parameters[i] == "R" || parameters[i] == "Y" || parameters[i] == "G" || parameters[i] == "B");
			if ((parameters[i] == "STUMBLE" || parameters[i] == "S") && parameters.Count() - i > 1)
            {
				yield return "sendtochaterror You cannot press another button after you stumble!";
				yield break;
			}
		}
		if (valid.Contains(false))
			yield break;
		yield return null;
		for (int i = 0; i < parameters.Count(); i++)
        {
			if (parameters[i] == "STUMBLE" || parameters[i] == "S")
				KeypadButtons[4].OnInteract();
			else if (parameters[i] == "RED" || parameters[i] == "R")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Red)].OnInteract();
			else if (parameters[i] == "YELLOW" || parameters[i] == "Y")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Yellow)].OnInteract();
			else if (parameters[i] == "GREEN" || parameters[i] == "G")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Green)].OnInteract();
			else if (parameters[i] == "BLUE" || parameters[i] == "B")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Blue)].OnInteract();
			else
				yield return "sendtochaterror Uh oh, that's not supposed to happen.";
			if (i + 1 != parameters.Count())
				yield return new WaitForSeconds(0.25f);
		}
		yield break;
    }

	IEnumerator TwitchHandleForcedSolve()
    {
		while (!moduleSolved)
        {
			while (animating) 
				yield return true;
			if (stumbleReqd || preStage)
				KeypadButtons[4].OnInteract();
			else if (previousColors[0] == startColors[0] && previousColors[1] == startColors[1] && previousColors[2] == startColors[2])
				KeypadButtons[Array.IndexOf(previousColors, Flashing[pressProgress])].OnInteract();
			else if (ColorPresses[pressProgress] != ColorType.White)
				KeypadButtons[Array.IndexOf(previousColors, ColorPresses[pressProgress])].OnInteract();
			else
				KeypadButtons[Array.IndexOf(baseDirections, table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])])].OnInteract();
			yield return new WaitForSeconds(0.25f);
		}
    }
}
