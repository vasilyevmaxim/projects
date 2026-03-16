using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureMapGenerator
{
    public class ProcedureGenerator
    {
        Chunk[,] Map;
        private Random random;

        public ProcedureGenerator(Chunk[,] Map)
        {
            this.Map = Map;
            this.random = new Random();
        }

        public ProcedureGenerator(int xLimit, int yLimit)
        {
            Map = new Chunk[xLimit, yLimit];
            this.random = new Random();
        }


        //Получение списка возможных направлений для роста
        private List<Tuple<ConnectionType, int, int>> GetPossibleDirections(int x, int y)
        {
            var directions = new List<Tuple<ConnectionType, int, int>>();
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            if (y > 0) directions.Add(new Tuple<ConnectionType, int, int>(ConnectionType.North, x, y - 1));
            if (y < height - 1) directions.Add(new Tuple<ConnectionType, int, int>(ConnectionType.South, x, y + 1));
            if (x > 0) directions.Add(new Tuple<ConnectionType, int, int>(ConnectionType.West, x - 1, y));
            if (x < width - 1) directions.Add(new Tuple<ConnectionType, int, int>(ConnectionType.East, x + 1, y));

            return directions;
        }

        //Случайное перемешивание направлений
        private void ShuffleDirections(List<Tuple<ConnectionType, int, int>> directions)
        {
            for (int i = directions.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = directions[i];
                directions[i] = directions[j];
                directions[j] = temp;
            }
        }

        //Получение противоположного направления
        private ConnectionType GetOppositeDirection(ConnectionType direction)
        {
            switch (direction)
            {
                case ConnectionType.North: return ConnectionType.South;
                case ConnectionType.South: return ConnectionType.North;
                case ConnectionType.West: return ConnectionType.East;
                case ConnectionType.East: return ConnectionType.West;
                case ConnectionType.Up: return ConnectionType.Down;
                case ConnectionType.Down: return ConnectionType.Up;
                default: return direction;
            }
        }
        public int GetGeneratedChunkCount()
        {
            int count = 0;
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (Map[x, y] != null)
                        count++;
                }
            }

            return count;
        }
        //Метод очистки карты
        public void ClearMap()
        {
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Map[x, y] = null;
                }
            }
        }
        public void PrintMapAsGraphCompact()
        {
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            Console.WriteLine("Визуализация карты:");

            //Создаем список строк для построчного вывода
            List<string> lines = new List<string>();

            //Проходим по всем строкам чанков
            for (int y = 0; y < height; y++)
            {
                string chunkLine = "";    //Линия с чанками
                string connectLine = "";  //Линия с соединениями

                for (int x = 0; x < width; x++)
                {
                    if (Map[x, y] != null)
                    {
                        //Добавляем чанк
                        
                        chunkLine += "#";

                        //Проверяем восточное соединение
                        if (x < width - 1 && Map[x + 1, y] != null &&
                            Map[x, y].directions[ConnectionType.East].Item1 &&
                            Map[x + 1, y].directions[ConnectionType.West].Item1)
                        {
                            chunkLine += " - ";
                        }
                        else
                        {
                            chunkLine += "   ";
                        }

                        //Для линии соединений проверяем южные соединения
                        if (y < height - 1 && Map[x, y + 1] != null &&
                            Map[x, y].directions[ConnectionType.South].Item1 &&
                            Map[x, y + 1].directions[ConnectionType.North].Item1)
                        {
                            connectLine += "|";
                        }
                        else
                        {
                            connectLine += " ";
                        }

                        //Заполняем пробелы между вертикальными соединениями
                        connectLine += "   ";
                    }
                    else
                    {
                        //Пустая ячейка
                        chunkLine += "    ";
                        connectLine += "    ";
                    }
                }

                //Добавляем линии в вывод
                if (!string.IsNullOrWhiteSpace(chunkLine))
                {
                    lines.Add(chunkLine.TrimEnd());
                }
                if (!string.IsNullOrWhiteSpace(connectLine))
                {
                    lines.Add(connectLine.TrimEnd());
                }
            }

            //Выводим все линии
            foreach (string line in lines)
            {
                Console.WriteLine(line);
            }
        }
        public void GenerateMap(int maxChunks = -1, double connectionChance = 0.7, int minExits = 2, int maxExits = 3)
        {
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            if (maxChunks <= 0)
            {
                maxChunks = width * height;
            }
            else
            {
                maxChunks = Math.Min(maxChunks, width * height);
            }

            ClearMap();

            int startX = width / 2;
            int startY = height / 2;

            //Создаем первый чанк с контролируемым количеством выходов
            Map[startX, startY] = CreateStartChunk(minExits, maxExits);
            int generatedChunks = 1;

            Queue<Tuple<int, int>> queue = new Queue<Tuple<int, int>>();
            queue.Enqueue(new Tuple<int, int>(startX, startY));

            while (queue.Count > 0 && generatedChunks < maxChunks)
            {
                var current = queue.Dequeue();
                int x = current.Item1;
                int y = current.Item2;
                Chunk currentChunk = Map[x, y];

                var allDirections = GetPossibleDirections(x, y);
                ShuffleDirections(allDirections);

                foreach (var directionInfo in allDirections)
                {
                    if (generatedChunks >= maxChunks)
                        break;

                    ConnectionType direction = directionInfo.Item1;
                    int newX = directionInfo.Item2;
                    int newY = directionInfo.Item3;

                    //Если целевая ячейка уже занята, синхронизируем с шансом
                    if (Map[newX, newY] != null)
                    {
                        if (random.NextDouble() < connectionChance)
                        {
                            EnsureConnection(currentChunk, direction, Map[newX, newY]);
                        }
                        continue;
                    }

                    //Создаем новое соединение с заданным шансом
                    if (!currentChunk.directions[direction].Item1)
                    {
                        if (random.NextDouble() < connectionChance) //Используем параметр
                        {
                            currentChunk.directions[direction] =
                                new Tuple<bool, bool?>(true, false);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (currentChunk.directions[direction].Item2 == true)
                        continue;

                    //Создаем новый чанк с контролируемым количеством выходов
                    Chunk newChunk = CreateConnectedChunk(currentChunk, direction, minExits, maxExits);
                    Map[newX, newY] = newChunk;
                    generatedChunks++;

                    currentChunk.directions[direction] =
                        new Tuple<bool, bool?>(true, true);

                    queue.Enqueue(new Tuple<int, int>(newX, newY));
                }

                if (queue.Count == 0 && generatedChunks < maxChunks)
                {
                    var borderPosition = FindBorderPosition();
                    if (borderPosition != null)
                    {
                        queue.Enqueue(borderPosition);
                    }
                }
            }

            Console.WriteLine($"Сгенерировано чанков: {generatedChunks}/{maxChunks}");
            Console.WriteLine($"Связность: {CalculateConnectivity():F2}%");
        }

        //Создание начального чанка (можно опустить метод, вынес для наглядности параметров)
        private Chunk CreateStartChunk(int minExits, int maxExits)
        {
            var directions = Chunk.NullDirections();
            var chunk = new Chunk(directions);
            return chunk.RandomizeDirection(minExits, maxExits);
        }
        //Создание чанка и подключение его
        private Chunk CreateConnectedChunk(Chunk connectedChunk, ConnectionType connectionDirection, int minExits, int maxExits)
        {
            var newDirections = Chunk.NullDirections();
            var oppositeDirection = GetOppositeDirection(connectionDirection);

            newDirections[oppositeDirection] = new Tuple<bool, bool?>(true, true);

            Chunk newChunk = new Chunk(newDirections);
            newChunk = newChunk.RandomizeDirection(minExits, maxExits);

            connectedChunk.directions[connectionDirection] = new Tuple<bool, bool?>(true, true);
            newChunk.directions[oppositeDirection] = new Tuple<bool, bool?>(true, true);

            return newChunk;
        }

        //Предустановленные параметры для генерации лабиринта
        public void GenerateMazeLikeMap(int maxChunks = -1)
        {
            //Для лабиринта используем меньше соединений
            GenerateMap(maxChunks, connectionChance: 0.4, minExits: 1, maxExits: 2);
        }
        //Предустановленные параметры для генерации туннелей
        public void GenerateTunnelMap(int tunnelCount = 3, int maxTunnelLength = 10)
        {
            //Для туннелей используем очень ограниченные соединения
            GenerateMap(tunnelCount * maxTunnelLength, connectionChance: 0.3, minExits: 1, maxExits: 2);
        }

        //Применение стиля при генерации
        public void GenerateMapWithStyle(MapStyle style, int maxChunks = -1)
        {
            switch (style)
            {
                case MapStyle.HighlyConnected:
                    GenerateMap(maxChunks, connectionChance: 0.8, minExits: 3, maxExits: 4);
                    break;
                case MapStyle.ModeratelyConnected:
                    GenerateMap(maxChunks, connectionChance: 0.6, minExits: 2, maxExits: 3);
                    break;
                case MapStyle.MazeLike:
                    GenerateMap(maxChunks, connectionChance: 0.4, minExits: 1, maxExits: 2);
                    break;
                case MapStyle.TunnelLike:
                    GenerateMap(maxChunks, connectionChance: 0.3, minExits: 1, maxExits: 2);
                    break;
                case MapStyle.Sparse:
                    GenerateMap(maxChunks, connectionChance: 0.2, minExits: 1, maxExits: 2);
                    break;
            }
        }

        //Перечисление для стилей карты
        public enum MapStyle
        {
            HighlyConnected,     //Сильно связная
            ModeratelyConnected, //Умеренно связная
            MazeLike,           //Лабиринтообразная
            TunnelLike,         //Туннельная
            Sparse              //Разреженная
        }
        //Гарантирует соединение между двумя чанками
        private void EnsureConnection(Chunk chunk1, ConnectionType dir1, Chunk chunk2)
        {
            ConnectionType dir2 = GetOppositeDirection(dir1);

            //Активируем выходы в обоих направлениях
            if (!chunk1.directions[dir1].Item1)
            {
                chunk1.directions[dir1] = new Tuple<bool, bool?>(true, true);
            }

            if (!chunk2.directions[dir2].Item1)
            {
                chunk2.directions[dir2] = new Tuple<bool, bool?>(true, true);
            }

            //Отмечаем как занятые
            chunk1.directions[dir1] = new Tuple<bool, bool?>(true, true);
            chunk2.directions[dir2] = new Tuple<bool, bool?>(true, true);
        }

        //Поиск граничной позиции (чанк, у которого есть соседние свободные ячейки)
        private Tuple<int, int> FindBorderPosition()
        {
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);

            List<Tuple<int, int>> borderPositions = new List<Tuple<int, int>>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (Map[x, y] != null)
                    {
                        //Проверяем есть ли свободные соседи у этого чанка
                        var neighbors = GetPossibleDirections(x, y);
                        if (neighbors.Any(n => Map[n.Item2, n.Item3] == null))
                        {
                            borderPositions.Add(new Tuple<int, int>(x, y));
                        }
                    }
                }
            }

            if (borderPositions.Count > 0)
            {
                return borderPositions[random.Next(borderPositions.Count)];
            }

            return null;
        }

        //Метод для расчета связности карты (процент соединенных чанков)
        private double CalculateConnectivity()
        {
            int width = Map.GetLength(0);
            int height = Map.GetLength(1);
            int totalChunks = GetGeneratedChunkCount();

            if (totalChunks <= 1) return 100.0;

            //Используем BFS для подсчета достижимых чанков из стартовой позиции
            bool[,] visited = new bool[width, height];
            int connectedCount = 0;

            //Находим первый чанк
            Tuple<int, int> start = null;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (Map[x, y] != null)
                    {
                        start = new Tuple<int, int>(x, y);
                        break;
                    }
                }
                if (start != null) break;
            }

            if (start == null) return 0.0;

            //BFS обход
            Queue<Tuple<int, int>> bfsQueue = new Queue<Tuple<int, int>>();
            bfsQueue.Enqueue(start);
            visited[start.Item1, start.Item2] = true;

            while (bfsQueue.Count > 0)
            {
                var current = bfsQueue.Dequeue();
                connectedCount++;

                int x = current.Item1;
                int y = current.Item2;
                Chunk chunk = Map[x, y];

                //Проверяем все направления
                if (y > 0 && chunk.directions[ConnectionType.North].Item1 &&
                    Map[x, y - 1] != null && !visited[x, y - 1])
                {
                    visited[x, y - 1] = true;
                    bfsQueue.Enqueue(new Tuple<int, int>(x, y - 1));
                }

                if (y < height - 1 && chunk.directions[ConnectionType.South].Item1 &&
                    Map[x, y + 1] != null && !visited[x, y + 1])
                {
                    visited[x, y + 1] = true;
                    bfsQueue.Enqueue(new Tuple<int, int>(x, y + 1));
                }

                if (x > 0 && chunk.directions[ConnectionType.West].Item1 &&
                    Map[x - 1, y] != null && !visited[x - 1, y])
                {
                    visited[x - 1, y] = true;
                    bfsQueue.Enqueue(new Tuple<int, int>(x - 1, y));
                }

                if (x < width - 1 && chunk.directions[ConnectionType.East].Item1 &&
                    Map[x + 1, y] != null && !visited[x + 1, y])
                {
                    visited[x + 1, y] = true;
                    bfsQueue.Enqueue(new Tuple<int, int>(x + 1, y));
                }
            }

            return (double)connectedCount / totalChunks * 100;
        }


    }
    public class Chunk
    {
        public Dictionary<ConnectionType, Tuple<bool, bool?>> directions;

        //Метод, создающий нулевые направления для чанка
        public static Dictionary<ConnectionType, Tuple<bool, bool?>> NullDirections()
        {
            Dictionary<ConnectionType, Tuple<bool, bool?>> res = new Dictionary<ConnectionType, Tuple<bool, bool?>>();
            res.Add(ConnectionType.North, new Tuple<bool, bool?>(false, null));
            res.Add(ConnectionType.South, new Tuple<bool, bool?>(false, null));
            res.Add(ConnectionType.West, new Tuple<bool, bool?>(false, null));
            res.Add(ConnectionType.East, new Tuple<bool, bool?>(false, null));
            res.Add(ConnectionType.Up, new Tuple<bool, bool?>(false, null));
            res.Add(ConnectionType.Down, new Tuple<bool, bool?>(false, null));
            return res;
        }

        public Chunk(Dictionary<ConnectionType, Tuple<bool, bool?>> dir)
        {
            directions = dir;
        }

        //Метод, случайным образом устанавливающий выходы
        public Chunk RandomizeDirection(int minPoints = 2, int maxPoints = 4)
        {
            Random rand = new Random();

            //Сначала отключаем все выходы
            foreach (var key in directions.Keys.ToList())
            {
                directions[key] = new Tuple<bool, bool?>(false, directions[key].Item2);
            }

            //Активируем случайное количество выходов (от minPoints до maxPoints)
            int targetExits = rand.Next(minPoints, maxPoints + 1);
            int currentExits = 0;

            //Получаем список доступных направлений
            var availableDirections = directions.Keys.ToList();

            while (currentExits < targetExits && availableDirections.Count > 0)
            {
                int randomIndex = rand.Next(availableDirections.Count);
                ConnectionType randomDirection = availableDirections[randomIndex];

                //Активируем этот выход, только если он не занят
                if (directions[randomDirection].Item2 != true)
                {
                    directions[randomDirection] = new Tuple<bool, bool?>(true, directions[randomDirection].Item2);
                    currentExits++;
                }

                availableDirections.RemoveAt(randomIndex);
            }
            return this;
        }
    }
    public enum ConnectionType
    {
        North, //Север
        South, //Юг
        West, //Запад
        East,  //Восток
        Up, //Верх
        Down //Низ
    }
}