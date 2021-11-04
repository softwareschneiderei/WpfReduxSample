using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfReduxSample.Reduxed;

namespace WpfReduxSample
{
    public class Selectors
    {
        private readonly SelectorGraph<State> graph;

        public ISelector<int> Counter { get; }
        public ISelector<IReadOnlyList<int>> Primes { get; }

        public Selectors(ReduxStore<State> store)
        {
            graph = store.SelectorGraph();

            Counter = graph.Create(state => state.Counter);
            Primes = graph.Create(Counter, counter => (IReadOnlyList<int>)ComputePrimes(counter).Reverse().ToList());
        }


        private int PrimeFactorOf(int number)
        {
            var bound = (int)Math.Sqrt(number);

            for (int i = 2; i <= bound; ++i)
            {
                if (number % i == 0)
                {
                    return i;
                }
            }
            return number;
        }

        private IEnumerable<int> ComputePrimes(int number)
        {
            if (number <= 1)
                return Array.Empty<int>();

            var factor = PrimeFactorOf(number);
            return ComputePrimes(number / factor).Append(factor);
        }
    }
}
