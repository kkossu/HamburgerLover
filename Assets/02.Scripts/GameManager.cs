﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	[SerializeField] float limitTime = 10f;
	[SerializeField] float leftTime = 0;

	[SerializeField] UILabel timer;
    [SerializeField] UISprite [] source;
    [SerializeField] UISprite [] target;

    bool started = false;
    HamburgerOrder hamburgerOrder = new HamburgerOrder();


    // Use this for initialization
    void Start()
	{
        _NewOrder();
        _ClearTarget();
    }
	
	void Update()
	{
		if (started)
		{
			_ReduceTime();
		}
	}

	void _ReduceTime()
	{
		leftTime -= Time.deltaTime;
		if (leftTime <= 0)
		{
			_StopGame();
		}

		_SetTimer(leftTime);
	}

	public void OnClickedStartGame()
	{
		_StartGame();
	}

	public void OnClickedBreadTop()
	{
	}

	public void OnClickedBreatBottom()
	{
	}

	public void OnClickedCheese()
	{
	}

	public void OnClickedCucumber()
	{
	}

	public void OnClickedCabage()
	{
	}

	public void OnClickedEgg()
	{
	}

	public void OnClickedTomato()
	{
	}

	public void OnClickedPatty()
	{
	}

	void _StartGame()
	{
		started = true;

		leftTime = limitTime;

		_SetTimer(leftTime);
	}

	void _StopGame()
	{
		started = false;

		leftTime = 0;

		_SetTimer(leftTime);
	}

	void _SetTimer(float time)
	{
		timer.text = string.Format("{0:F2} s", time);
	}

    void _NewOrder()
    {
        List<Ingredient> newOrder = hamburgerOrder.GetOrder();

        for (int index = 0; index < source.Length; ++index)
        {
            if (index < newOrder.Count)
            {
                source[index].spriteName = newOrder[index].ToString().ToLower();
            }
            else
            {
                source[index].spriteName = "";
            }
        }
    }

    void _ClearTarget()
    {
        foreach(var entry in target)
        {
            entry.spriteName = "";
        }
    }
}
