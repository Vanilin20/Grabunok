using Banda.Core;

namespace Banda.Heist
{
    /// <summary>
    /// Допоміжне для головоломки з дротами. Лівий дріт i має колір i; праві гнізда мають кольори <c>right[slot]</c>.
    /// Розвʼязок — для кожного дроту номер гнізда з таким самим кольором. Рахує й клієнт (автоперевірка, боти, тести);
    /// авторитетна перевірка — тільки в HeistSim.
    /// </summary>
    public static class HackPuzzle
    {
        /// <summary>Правильні гнізда для всіх дротів.</summary>
        public static int[] Solution(int[] right)
        {
            int[] sockets = new int[right.Length];
            for (int slot = 0; slot < right.Length; slot++)
            {
                if (right[slot] >= 0 && right[slot] < sockets.Length)
                {
                    sockets[right[slot]] = slot;
                }
            }

            return sockets;
        }

        /// <summary>Хакеру з тестів/ботів: узятися за ціль, розвʼязати й надіслати. Повертає false, якщо хост відхилив старт.</summary>
        public static bool SolveNow(HeistSim sim, int target)
        {
            if (!sim.Apply(RoleId.Hacker, HeistAction.HackStart(target)))
            {
                return false;
            }

            return sim.Apply(RoleId.Hacker, HeistAction.HackSolve(Solution(sim.HackRight)));
        }
    }
}
