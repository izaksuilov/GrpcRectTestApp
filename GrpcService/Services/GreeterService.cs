using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService;
using System.Threading;

namespace GrpcService.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private const int MIN_THREADS = 5, MAX_THREADS = 10,
                          MIN_RECTS = 20, MAX_RECTS = 100,
                          MIN_RECT_WIDTH = 10, MAX_RECT_WIDTH = 40,
                          MIN_RECT_HEIGHT = 10, MAX_RECT_HEIGHT = 40,
                          FIELD_HEIGHT = 576, FIELD_WIDTH = 1024;

        private const double MIN_DELTA_X = 0.4, MAX_DELTA_X = 1.8,
                             MIN_DELTA_Y = 0.4, MAX_DELTA_Y = 1.8;

        private static ServerRect[]? _rectangles;
        private volatile static Rectangle EmptyRect = new();
        private static CancellationTokenSource? _cancelTokenSource;
        private static CancellationToken _token;
        private static ManualResetEvent _arrayReady;
        private static List<Thread> _threads;
        private static ReadWriteLock _locker;
        public override Task<State> Start(Empty request, ServerCallContext context)
        {
            bool result = false;
            if (_cancelTokenSource == null)//если значение не null, значит, уже запущено
            {
                try
                {
                    Init();
                    result = true;
                }
                catch (Exception ex)
                {
                    result = false;
                }
            }
            return Task.FromResult(new State() { OperationSucess = result });

        }
        public override Task<State> Stop(Empty request, ServerCallContext context)
        {
            bool result = false;
            try
            {
                if (_cancelTokenSource == null || _threads == null || _threads.Count == 0)
                    result = false;
                else
                {
                    _cancelTokenSource!.Cancel();
                    foreach (var t in _threads)
                        t.Join();
                    _cancelTokenSource.Dispose();
                    _cancelTokenSource = null;
                    _threads.Clear();
                    result = true;
                }
            }
            catch (Exception ex)
            {
                result = false;
            }
            return Task.FromResult(new State() { OperationSucess = result });
        }
        private static void Init()
        {
            _locker = new ReadWriteLock();
            _cancelTokenSource = new CancellationTokenSource();
            _token = _cancelTokenSource.Token;
            _threads = new List<Thread>();
            _arrayReady = new ManualResetEvent(false);
            _rectangles = null;

            //запускаем потоки
            int threads = new Random().Next(MIN_THREADS, MAX_THREADS);
            int totalRects = 0;
            for (int i = 0; i < threads; i++)
            {
                int currentThreadRects = new Random().Next(MIN_RECTS, MAX_RECTS);
                Thread t = new Thread(RectProcess);
                _threads.Add(t);
                t.Start(new Tuple<int, int>(totalRects, currentThreadRects));
                totalRects += currentThreadRects;
            }
            //создаем массив с заполнеными изначальными значениями 
            _rectangles = Enumerable.Repeat(new ServerRect(new Rectangle(), 0, 0, false), totalRects).ToArray();
            _arrayReady.Set();
        }

        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        private static void RectProcess(object? param)
        {
            Random random = new Random();

            Tuple<int, int> tuple = (param as Tuple<int, int>)!;
            int startIxd = tuple.Item1;
            int currentThreadRects = tuple.Item2;
            int endIdx = startIxd + currentThreadRects;

            //ожидаем заполнения всего массива,
            //тк каждый поток работает со своим диапазоном в массиве

            _arrayReady.WaitOne();
            if (_rectangles == null)
                return;

            ServerRect[] rectangles = new ServerRect[endIdx - startIxd];

            //создаем прямоугольники
            for (int i = 0; i < rectangles.Length; i++)
            {
                Rectangle rect = new Rectangle()
                {
                    Height = random.Next(MIN_RECT_HEIGHT, MAX_RECT_HEIGHT),
                    Width = random.Next(MIN_RECT_WIDTH, MAX_RECT_WIDTH)
                };
                rect.X = random.Next(0, (int)(FIELD_WIDTH - rect.Width));
                rect.Y = random.Next(0, (int)(FIELD_HEIGHT - rect.Height));

                rectangles[i] = new ServerRect(
                    rect,
                    random.NextDouble() * (random.NextDouble() > 0.5 ? 1 : -1),
                    random.NextDouble() * (random.NextDouble() > 0.5 ? 1 : -1),
                    true);
            }

            using (_locker.WriteLock())//перезаписываем только измененный диапазон
            {
                for (int i = startIxd, j = 0; i < endIdx; i++, j++)
                    _rectangles[i] = rectangles[j];
            }

            //обновялем прямоугольники
            while (!_token.IsCancellationRequested)
            {
                using (_locker.ReadLock())
                {
                    rectangles = _rectangles[startIxd..endIdx];
                }
                for (int i = 0; i < endIdx - startIxd; i++)
                {
                    //Здесь не используется ResetEvent, тк может возникуть ситуация, 
                    //когда rectangles[i] еще не отправлен, а rectangles[i + 1] уже отправлен.
                    //В таком случае нам не нужно ждать текущий прямоугольник, а можно перейти к следующему
                    if (rectangles[i].IsCalculated)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    double deltaX = rectangles[i].DeltaX, deltaY = rectangles[i].DeltaY;

                    double offsetX = rectangles[i].Rect.X + deltaX;
                    double offsetY = rectangles[i].Rect.Y + deltaY;

                    double CheckAndCorrectPosition(double delta, double min, double max)
                    {
                        delta *= -1;
                        int deltaSign = delta > 0 ? 1 : -1;
                        delta = Math.Abs(delta) + random.NextDouble() * (random.NextDouble() > 0.5 ? 1 : -1);

                        if (delta < min)
                            delta = min;
                        else if (delta > max)
                            delta = max;

                        return delta * deltaSign;
                    }

                    if (offsetX < 0 || offsetX + rectangles[i].Rect.Width >= FIELD_WIDTH)
                        rectangles[i].DeltaX = CheckAndCorrectPosition(deltaX, MIN_DELTA_X, MAX_DELTA_X);

                    if (offsetY < 0 || offsetY + rectangles[i].Rect.Height >= FIELD_HEIGHT)
                        rectangles[i].DeltaY = CheckAndCorrectPosition(deltaY, MIN_DELTA_Y, MAX_DELTA_Y);


                    rectangles[i].Rect.X += rectangles[i].DeltaX;
                    rectangles[i].Rect.Y += rectangles[i].DeltaY;

                    rectangles[i].IsCalculated = true;
                }
                using (_locker.WriteLock())//перезаписываем только измененный диапазон
                {
                    for (int i = startIxd, j = 0; i < endIdx; i++, j++)
                        _rectangles[i] = rectangles[j];
                }
            }
        }
        public override Task<Size> GetFieldSize(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Size()
            {
                Height = FIELD_HEIGHT,
                Width = FIELD_WIDTH,
            });
        }

        public override async Task GetArrayRect(Empty request,
            IServerStreamWriter<Rectangle> responseStream,
            ServerCallContext context)
        {
            ServerRect[] rectangles;

            using (_locker.ReadLock())
            {
                if (_rectangles == null)
                    return;
                rectangles = (ServerRect[])_rectangles.Clone();
            }
            foreach (var serverRect in rectangles)
            {
                if (_token.IsCancellationRequested)
                    break;
                //для непосчитанного прямоугольника можно было бы отправлять null, чтобы сэкономить время
                //но null недопустим в данной ситуации, тк будет вылетать ошибка
                if (!serverRect.IsCalculated)
                    await responseStream.WriteAsync(EmptyRect).ConfigureAwait(false);
                else
                    await responseStream.WriteAsync(serverRect.Rect).ConfigureAwait(false);
            }
            using (_locker.WriteLock())
            {
                for (int i = 0; i < rectangles.Length; i++)//помечаем только отправленные объекты 
                {
                    if (rectangles[i].IsCalculated)
                        _rectangles[i].IsCalculated = false;
                }
            }
        }


        class ServerRect
        {
            public Rectangle Rect;
            public double DeltaX;
            public double DeltaY;
            public bool IsCalculated;

            public ServerRect(Rectangle rect, double deltaX, double deltaY, bool isCalculated)
            {
                Rect = rect;
                DeltaX = deltaX;
                DeltaY = deltaY;
                IsCalculated = isCalculated;
            }
        }
    }
}