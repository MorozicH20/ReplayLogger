using System;
using UnityEngine;
public class NumberInCanvas
{
    public int Number { get; private set; }
    public Color Color { get; private set; }
    public int Seed { get; private set; }

    public NumberInCanvas(int initialSeed)
    {
        Seed = initialSeed;
        GenerateNumberAndColor(initialSeed);
    }

    public void NextGeneration(string keyPressed)
    {
        Seed = CalculateNewSeed(Seed, keyPressed.GetHashCode());
        GenerateNumberAndColor(Seed);
    }

    private void GenerateNumberAndColor(int seed)
    {
        System.Random prng = new System.Random(seed);

        Number = prng.Next(10000000, 99999999);
        Color = new Color((float)prng.NextDouble(), (float)prng.NextDouble(), (float)prng.NextDouble(),0.9f);
    }

    private int CalculateNewSeed(int currentSeed, int keyModifier)
    {
        return currentSeed * 31 + keyModifier;
    }



}