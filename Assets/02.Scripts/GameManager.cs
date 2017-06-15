using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	[SerializeField] float limitTime = 10f;
	[SerializeField] float leftTime = 0;

	[SerializeField] UILabel timer;

	bool started = false;


	// Use this for initialization
	void Start()
	{
		
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

}
