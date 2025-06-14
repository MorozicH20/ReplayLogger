using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegitimateChallenge
{
    partial class LoadingSequenceGenerator
    {
        private Random _random;
        private int _minDuration;
        private int _maxDuration;
        private (bool, int)? _currentState = null;

        public LoadingSequenceGenerator(int? seed = null, int minDuration = 5, int maxDuration = 30)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _minDuration = minDuration;
            _maxDuration = maxDuration;
        }

        public (bool, int) GenerateNext((bool, int)? previousState = null)
        {
            (bool, int) newState;
            if (previousState == null)
            {
                // Генерация начального состояния
                bool newBool = _random.Next(2) == 0; // 0 или 1 для выбора bool
                int newInt = _random.Next(_minDuration, _maxDuration + 1); // +1 потому что Next(min, max) возвращает [min, max)
                newState = (newBool, newInt);
            }
            else
            {
                // Генерация на основе предыдущего состояния
                (bool prevBool, int prevInt) = previousState.Value;

                // Инвертируем bool значение с некоторой вероятностью
                bool newBool = (_random.NextDouble() < 0.7) ? !prevBool : prevBool;

                // Модифицируем длительность, но не сильно
                int change = _random.Next(-5, 6); // [min, max)
                int newInt = Math.Max(_minDuration, Math.Min(_maxDuration, prevInt + change));

                newState = (newBool, newInt);
            }

            _currentState = newState;
            _sequence.Add(newState);
            return newState;
        }

    }
}
