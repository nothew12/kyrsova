using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfIsland
{
    public enum Sex { Male, Female }

    public class Wolf
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Points { get; set; } = 1.0;
        public Sex Sex { get; }
        public bool Alive => Points > 0;
        public Wolf(int x, int y, Sex sex) { X = x; Y = y; Sex = sex; }
    }

    public class SimulationGrid
    {
        public const int Size = 20;

        private bool[,] _rabbits = new bool[Size, Size];
        private List<Wolf> _wolves = new();
        private Random _rng;

        public int RabbitCount { get; private set; }
        public int WolfCount => _wolves.Count(w => w.Alive && w.Sex == Sex.Male);
        public int WolfessCount => _wolves.Count(w => w.Alive && w.Sex == Sex.Female);
        public IReadOnlyList<Wolf> Wolves => _wolves;
        public bool HasRabbit(int x, int y) => _rabbits[x, y];

        public SimulationGrid(Random rng) { _rng = rng; }

        public void PlaceRabbit(int x, int y)
        {
            if (!_rabbits[x, y]) { _rabbits[x, y] = true; RabbitCount++; }
        }

        public void AddWolf(Wolf w) => _wolves.Add(w);

        public List<(int, int)> Neighbors(int x, int y)
        {
            var result = new List<(int, int)>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < Size && ny >= 0 && ny < Size)
                        result.Add((nx, ny));
                }
            return result;
        }

        private bool EatRabbit(int x, int y)
        {
            if (_rabbits[x, y]) { _rabbits[x, y] = false; RabbitCount--; return true; }
            return false;
        }

        private bool HasFemaleWolf(int x, int y) =>
            _wolves.Any(w => w.Alive && w.Sex == Sex.Female && w.X == x && w.Y == y);

        private (int, int)? FindAdjacentRabbit(int x, int y)
        {
            foreach (var (nx, ny) in Neighbors(x, y))
                if (_rabbits[nx, ny]) return (nx, ny);
            return null;
        }

        private (int, int)? FindAdjacentFemale(int x, int y)
        {
            foreach (var (nx, ny) in Neighbors(x, y))
                if (HasFemaleWolf(nx, ny)) return (nx, ny);
            return null;
        }

        public void Step()
        {
            var rabbitList = new List<(int, int)>();
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                    if (_rabbits[i, j]) rabbitList.Add((i, j));

            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                    _rabbits[i, j] = false;
            RabbitCount = 0;

            var newRabbits = new List<(int, int)>();
            foreach (var (x, y) in rabbitList)
            {
                var moves = Neighbors(x, y);
                moves.Add((x, y));
                var (nx, ny) = moves[_rng.Next(moves.Count)];
                PlaceRabbit(nx, ny);
                if (_rng.NextDouble() < 0.2)
                    newRabbits.Add((x, y));
            }
            foreach (var (nx, ny) in newRabbits) PlaceRabbit(nx, ny);

            foreach (var wolf in _wolves.Where(w => w.Alive).OrderBy(_ => _rng.Next()).ToList())
            {
                if (wolf.Sex == Sex.Female)
                {
                    var rpos = FindAdjacentRabbit(wolf.X, wolf.Y);
                    if (rpos.HasValue)
                    {
                        wolf.X = rpos.Value.Item1; wolf.Y = rpos.Value.Item2;
                        wolf.Points += EatRabbit(wolf.X, wolf.Y) ? 1.0 : -0.1;
                    }
                    else
                    {
                        var moves = Neighbors(wolf.X, wolf.Y);
                        if (moves.Count > 0) { var m = moves[_rng.Next(moves.Count)]; wolf.X = m.Item1; wolf.Y = m.Item2; }
                        wolf.Points -= 0.2;
                    }
                }
                else
                {
                    var rpos = FindAdjacentRabbit(wolf.X, wolf.Y);
                    if (rpos.HasValue)
                    {
                        wolf.X = rpos.Value.Item1; wolf.Y = rpos.Value.Item2;
                        wolf.Points += EatRabbit(wolf.X, wolf.Y) ? 1.0 : -0.1;
                    }
                    else
                    {
                        var fpos = FindAdjacentFemale(wolf.X, wolf.Y);
                        if (fpos.HasValue) { wolf.X = fpos.Value.Item1; wolf.Y = fpos.Value.Item2; }
                        else
                        {
                            var moves = Neighbors(wolf.X, wolf.Y);
                            if (moves.Count > 0) { var m = moves[_rng.Next(moves.Count)]; wolf.X = m.Item1; wolf.Y = m.Item2; }
                        }
                        wolf.Points -= 0.2;
                    }
                }
            }

            var males = _wolves.Where(w => w.Alive && w.Sex == Sex.Male).ToList();
            var females = _wolves.Where(w => w.Alive && w.Sex == Sex.Female).ToList();
            foreach (var male in males)
            {
                var partner = females.FirstOrDefault(f => f.Alive && f.X == male.X && f.Y == male.Y && !_rabbits[f.X, f.Y]);
                if (partner != null)
                {
                    var sex = _rng.NextDouble() < 0.5 ? Sex.Male : Sex.Female;
                    _wolves.Add(new Wolf(male.X, male.Y, sex));
                    females.Remove(partner);
                }
            }

            _wolves.RemoveAll(w => !w.Alive);
        }

        public static SimulationGrid CreateDefault(Random rng)
        {
            var g = new SimulationGrid(rng);
            var used = new HashSet<(int, int)>();

            int placed = 0;
            while (placed < 20)
            {
                int x = rng.Next(Size), y = rng.Next(Size);
                if (!g.HasRabbit(x, y)) { g.PlaceRabbit(x, y); placed++; }
            }

            void PlaceWolf(Sex sex)
            {
                int x, y;
                do { x = rng.Next(Size); y = rng.Next(Size); } while (used.Contains((x, y)));
                used.Add((x, y));
                g.AddWolf(new Wolf(x, y, sex));
            }
            for (int i = 0; i < 3; i++) PlaceWolf(Sex.Male);
            for (int i = 0; i < 3; i++) PlaceWolf(Sex.Female);
            return g;
        }
    }
}
