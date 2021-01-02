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
	private Coroutine[] FlashWaitRoutines = new Coroutine[2];
	private int stage = 1;
	private int pressProgress = 0;
	private bool preStage = true;
	private ColorType[] Flashing = new ColorType[5];
	private Direction[] Sounds = new Direction[5];
	private ColorType[] ColorPresses = new ColorType[5] { ColorType.White, ColorType.White, ColorType.White, ColorType.White, ColorType.White };
	private bool stumbleReqd = false;

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
			startColors[i] = previousColors[i];
			ButtonLights[i].enabled = false;
			ButtonLights[i].range = transform.lossyScale.x;
			if (Blind.ColorblindModeActive)
				CBTexts[i].text = previousColors[i].ToString();
		}
		animating = false;

		for (int i = 0; i < 5; i++)
        {
			Flashing[i] = baseColors[Rnd.Range(0, 4)];
			Sounds[i] = baseDirections[Rnd.Range(0, 4)];
		}
		Log("The order of flashing colors is " + Flashing[0].ToString() + ", " + Flashing[1].ToString() + ", " + Flashing[2].ToString() + ", " + Flashing[3].ToString() + ", and" + Flashing[4].ToString() + ".");


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
		bool unicorn = false;
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
		int index = Array.IndexOf(KeypadButtons, Button);
		if (index == 4)
		{
			if (preStage)
			{
				Log("Stumble pressed. Let's see how this goes, shall we?");
				StartCoroutine(Stumble(null));
				preStage = false;
				FlashWaitRoutines[0] = StartCoroutine(FlashColors());
				Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
				Button.AddInteractionPunch();
				Audio.PlaySoundAtTransform(SoundEffects[4].name, transform);
				return;
			}
			else if (stumbleReqd)
			{
				DebugLog("Stumble pressed, and you needed to. Nice!");
				StartCoroutine(Stumble(null));
				stumbleReqd = false;
			}
			else
			{
				DebugLog("Stumble pressed, but you didn't need to. Strike!");
				Strike();
				return;
			}
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			Button.AddInteractionPunch();
			Audio.PlaySoundAtTransform(SoundEffects[4].name, transform);
			stumbleReqd = false;
		}
		else
		{
			if (preStage)
				return;
			//it's a button, so we need to check the press. 
			if (stumbleReqd)
            {
				Log("You needed to press stumble, but you didn't. Strike!");
				Strike();
				return;
            }
			Log("You pressed the " + baseDirections[index].ToString() + " button, which was colored " + previousColors[index].ToString() + ".");
			DebugLog("Direction Pressed: " + baseDirections[index].ToString() + " | Table Lookup: " + table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])].ToString() + " | ");
			DebugLog("Row Lookup: " + Array.IndexOf(baseColors, Flashing[pressProgress]).ToString() + " | Column Lookup: " + Array.IndexOf(previousColors, Flashing[pressProgress]).ToString());
			if (previousColors == startColors)
            {
				Log("Currently displayed colors are what was initially shown, using the 'However' condition.");
				unicorn = true;
				if (Flashing[pressProgress] == previousColors[index])
					Log("You pressed the flashing color, correct!");
				else
                {
					Log("You did not press the flashing color, Strike!");
					Strike();
					return;
                }
            }
			else if ((baseDirections[index] == table[Array.IndexOf(baseColors, Flashing[pressProgress])][Array.IndexOf(previousColors, Flashing[pressProgress])] && ColorPresses[pressProgress] == ColorType.White) || ColorPresses[pressProgress] == previousColors[index])
				Log("Correct, probably");
			else
			{
				Log("Not correct, probably. Strike!");
				Strike();
				return;
			}
			if ((previousColors[index] == Flashing[pressProgress] || Array.IndexOf(baseDirections, Sounds[pressProgress]) == index) && !unicorn)
			{
				stumbleReqd = true;
				ColorPresses[pressProgress] = ColorType.White;
			}
			else
				ColorPresses[pressProgress] = previousColors[index];
			pressProgress++;
			if (unicorn && pressProgress == stage)
				stumbleReqd = true;
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			Button.AddInteractionPunch();
			FlashWaitRoutines[1] = StartCoroutine(FlashButton(index));
		}
		if (pressProgress == stage && !stumbleReqd)
        {
			stage++;
			pressProgress = 0;
			Log("Stage " + (stage - 1).ToString() + " completed, proceeding...");
			if (stage > 5)
            {
				Log("All inputs entered, module solved.");
				Module.HandlePass();
				return;
            }
			else
				FlashWaitRoutines[0] = StartCoroutine(FlashColors());
		}
    }
	void Strike()
    {
		for (int i = 0; i < 4; i++)
			ButtonLights[i].enabled = false;
		StartCoroutine(Stumble(startColors));
		for (int i = 0; i < 5; i++)
			ColorPresses[i] = ColorType.White;
		DebugLog("Strike detected, outputting pressProgress: " + pressProgress.ToString() + " (default 0).");
		preStage = true;
		stumbleReqd = false;
		pressProgress = 0;
		Module.HandleStrike();
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
				ButtonMats[i].GetComponent<MeshRenderer>().material.color = Color.Lerp(ColorValues[previousColors[i]], ColorValues[toSet[i]], amountChanged);
			yield return null;
		}
		for (int i = 0; i < 4; i++)
        {
			previousColors[i] = toSet[i];
			if (Blind.ColorblindModeActive)
				CBTexts[i].text = toSet[i].ToString();
        }
		animating = false;
		yield return null;
    }

	IEnumerator FlashColors()
    {
		while (true)
        {
			while (animating)
				yield return new WaitForSeconds(0.01f);
			for (int i = 0; i < stage; i++)
            {
				ButtonLights[Array.IndexOf(previousColors, Flashing[i])].color = ColorValues[Flashing[i]];
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
		yield return new WaitForSeconds(0.5f);
		ButtonLights[index].enabled = false;
		yield break;
    }

	public readonly string TwitchHelpMessage = "Press the buttons using !{0} <button>. Valid buttons are Red, Yellow, Green, Blue, and Stumble. Buttons are chainable with spaces.";
	IEnumerator ProcessTwitchCommand(string command)
    {
		while (animating) yield return new WaitForSeconds(0.01f);
		string[] parameters = command.ToUpperInvariant().Trim().Split(' ').ToArray();
		if (parameters.Count() < 1 || parameters.Count() > 6)
			yield break;
		bool[] valid = new bool[parameters.Count()];
		for (int i = 0; i < parameters.Count(); i++)
        {
			valid[i] = (parameters[i] == "STUMBLE" || parameters[i] == "RED" || parameters[i] == "YELLOW" || parameters[i] == "GREEN" || parameters[i] == "BLUE");
			if (parameters[i] == "STUMBLE" && parameters.Count() - i > 1)
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
			if (parameters[i] == "STUMBLE")
				KeypadButtons[4].OnInteract();
			else if (parameters[i] == "RED")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Red)].OnInteract();
			else if (parameters[i] == "YELLOW")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Yellow)].OnInteract();
			else if (parameters[i] == "GREEN")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Green)].OnInteract();
			else if (parameters[i] == "BLUE")
				KeypadButtons[Array.IndexOf(previousColors, ColorType.Blue)].OnInteract();
			else
				yield return "sendtochaterror Uh oh, that's not supposed to happen.";
			if (i + 1 != parameters.Count())
				yield return new WaitForSeconds(0.25f);
		}
		yield break;
    }
}
