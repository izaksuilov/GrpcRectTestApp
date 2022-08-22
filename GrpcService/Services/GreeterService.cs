using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService;
using System.Threading;

namespace GrpcService.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private const int MIN_THREADS = 5,      MAX_THREADS = 10,
                          MIN_RECTS = 20,       MAX_RECTS = 100,
                          MIN_RECT_WIDTH = 10,  MAX_RECT_WIDTH = 40,
                          MIN_RECT_HEIGHT = 10, MAX_RECT_HEIGHT = 40,
                          FIELD_HEIGHT = 576,   FIELD_WIDTH = 1024;

        private const double MIN_DELTA_X = 0.4, MAX_DELTA_X = 1.8,
                             MIN_DELTA_Y = 0.4, MAX_DELTA_Y = 1.8;

        private volatile static ServerRect[]? _rectangles;
        private volatile static Rectangle EmptyRect = new();
        private static CancellationTokenSource? _cancelTokenSource;
        private static CancellationToken _token;
        private static ManualResetEvent _arrayReady;
        private static List<Thread> _threads;
        public override Task<State> Start(Empty request, ServerCallContext context)
        {
            bool result = false;
            if (_cancelTokenSource == null)
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

            _arrayReady.WaitOne();

            //создаем прямоугольники
            for (int i = startIxd; i < endIdx; i++)
            {
                Rectangle rect = new Rectangle()
                {
                    Height = random.Next(MIN_RECT_HEIGHT, MAX_RECT_HEIGHT),
                    Width = random.Next(MIN_RECT_WIDTH, MAX_RECT_WIDTH)
                };
                rect.X = random.Next(0, (int)(FIELD_WIDTH - rect.Width));
                rect.Y = random.Next(0, (int)(FIELD_HEIGHT - rect.Height));

                _rectangles![i] = new ServerRect(
                    rect,
                    random.NextDouble() * (random.NextDouble() > 0.5 ? 1 : -1),
                    random.NextDouble() * (random.NextDouble() > 0.5 ? 1 : -1),
                    true);
            }

            while (!_token.IsCancellationRequested)
            {
                for (int i = startIxd; i < endIdx; i++)
                {
                    if (_rectangles![i].IsCalculated)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    double deltaX = _rectangles[i].DeltaX, deltaY = _rectangles[i].DeltaY;

                    double offsetX = _rectangles[i].Rect.X + deltaX;
                    double offsetY = _rectangles[i].Rect.Y + deltaY;

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

                    if (offsetX < 0 || offsetX + _rectangles[i].Rect.Width >= FIELD_WIDTH)
                        _rectangles[i].DeltaX = CheckAndCorrectPosition(deltaX, MIN_DELTA_X, MAX_DELTA_X);

                    if (offsetY < 0 || offsetY + _rectangles[i].Rect.Height >= FIELD_HEIGHT)
                        _rectangles[i].DeltaY = CheckAndCorrectPosition(deltaY, MIN_DELTA_Y, MAX_DELTA_Y);
                    

                    _rectangles[i].Rect.X += _rectangles[i].DeltaX;
                    _rectangles[i].Rect.Y += _rectangles[i].DeltaY;

                    _rectangles[i].IsCalculated = true;
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
            if (_rectangles == null)
                return;

            for (int i = 0; i < _rectangles.Length; i++)
            {
                if (_token.IsCancellationRequested)
                    break;

                if (!_rectangles[i].IsCalculated)
                    await responseStream.WriteAsync(EmptyRect).ConfigureAwait(false);
                else
                {
                    await responseStream.WriteAsync(_rectangles[i].Rect).ConfigureAwait(false);
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