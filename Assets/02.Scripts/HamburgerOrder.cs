using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HamburgerOrder
{
    public const int maxIngredient = 10;
    int level = 1;
    int ingredient = 0;
 

    public List<Ingredient> GetOrder()
	{
        List<Ingredient> burger = new List<Ingredient>();

        ingredient = _GetIngrediantCount();

        burger.Add(Ingredient.BreadBottom);
        for(int index = 0; index < ingredient; ++index)
        {
            int material = Random.Range((int)Ingredient.Cheese, (int)Ingredient.End);

            burger.Add((Ingredient)material);
        }
        burger.Add(Ingredient.BreadTop);

        return burger;
    }

    int _GetIngrediantCount()
    {
        if (level < 5)
        {
            return Random.Range(3, 5);
        }
        else if (level < 10)
        {
            return Random.Range(4, 6);
        }
        else if (level < 15)
        {
            return Random.Range(5, 7);
        }
        else if (level < 20)
        {
            return Random.Range(5, 8);
        }
        else
        {
            return Random.Range(6, maxIngredient);
        }
    }
	void _GetRandomIngredient()
	{
		
	}
}


public enum Ingredient
{
	BreadTop,
	BreadBottom,
	Cheese,
	Cabage,
	Cucumber,
	Egg,
	Tomato,
	Patty,

    End
}
