﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TelemetryDemoDie : MonoBehaviour
{
    public Text nameField;
    public TelemetryDie graphs;
    public RawImage dieImage;
    public Die3D die3D;
    public Button changeColorButton;
    public Button showOffButton;
    public Button showOff2Button;
    public Text faceNumberText;

    Die die;


    private void Awake()
    {
    }
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (die.face != -1)
            faceNumberText.text = (die.face).ToString();
        else
            faceNumberText.text = "";
    }

    public void Setup(Die die)
    {
        if (this.die != null)
        {
            this.die.OnSettingsChanged -= OnDieSettingsChanged;
        }

        nameField.text = die.name;
        graphs.Setup(die.name);
        var rt = die3D.Setup(1);
        dieImage.texture = rt;

        this.die = die;
        this.die.OnSettingsChanged += OnDieSettingsChanged;

        changeColorButton.onClick.RemoveAllListeners();
        changeColorButton.onClick.AddListener(ChangeColor);

        showOffButton.onClick.RemoveAllListeners();
        showOffButton.onClick.AddListener(() => ShowOff(1));

        showOff2Button.onClick.RemoveAllListeners();
        showOff2Button.onClick.AddListener(() => ShowOff(0));
    }

    public void OnTelemetryReceived(Vector3 acc, int millis)
    {
        graphs.OnTelemetryReceived(acc, millis);
        die3D.UpdateAcceleration(acc);

    }

    void OnDieSettingsChanged(Die die)
    {
        nameField.text = die.name;
    }

    void ChangeColor()
    {
        var color = die.SetNewColor();
        die3D.pipsColor = color;
        faceNumberText.color = color;
    }

    void ShowOff(int index)
    {
        die.Flash(index);
    }
}