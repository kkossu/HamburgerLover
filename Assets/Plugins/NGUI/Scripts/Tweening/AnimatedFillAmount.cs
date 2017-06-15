//----------------------------------------------
//            NGUI: Next-Gen UI kit
// Copyright © 2011-2016 Tasharen Entertainment
//----------------------------------------------

using UnityEngine;

/// <summary>
/// Makes it possible to animate alpha of the widget or a panel.
/// </summary>

[ExecuteInEditMode]
public class AnimatedFillAmount : MonoBehaviour
{
	[Range(0f, 1f)]
	public float fillAmount = 1f;

    UIBasicSprite mSprite;

	void OnEnable ()
	{
		mSprite = GetComponent<UIBasicSprite>();
		LateUpdate();
	}

	void LateUpdate ()
	{
		if (mSprite != null) mSprite.fillAmount = fillAmount;
	}
}
