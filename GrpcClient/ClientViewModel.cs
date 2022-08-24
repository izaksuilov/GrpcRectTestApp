using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GrpcClient
{
    public class ClientViewModel : INotifyPropertyChanged
    {
        public ClientViewModel()
        {
            Rectangles = new ObservableCollection<RectangleWithColor>();
            Client = new Client();
            Client.Connect();
        }

        #region Members and fields

        public ObservableCollection<RectangleWithColor> Rectangles { get; set; }
        public SolidColorBrush BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                RaisePropertyChanged(nameof(BorderColor));
            }
        }
        public Thickness BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = value;
                RaisePropertyChanged(nameof(BorderThickness));
            }
        }
        public double BorderWidth
        {
            get => _borderWidth;
            set
            {
                _borderWidth = value;
                RaisePropertyChanged(nameof(BorderWidth));
            }
        }
        public double BorderHeight
        {
            get => _borderHeight;
            set
            {
                _borderHeight = value;
                RaisePropertyChanged(nameof(BorderHeight));
            }
        }

        private SolidColorBrush _borderColor;
        private Thickness _borderThickness;
        private double _borderWidth;
        private double _borderHeight;

        private Client Client;
        private CancellationTokenSource? _cancelTokenSource;
        private CancellationToken _token;
        private AutoResetEvent _cancel;

        #endregion

        public async void Start()
        {
            if (!Client.Start())
                return;
            _cancelTokenSource = new CancellationTokenSource();
            _token = _cancelTokenSource.Token;
            _cancel = new AutoResetEvent(false);

            var reply = Client.GetFieldSize();

            BorderThickness = new Thickness(5);
            BorderWidth = reply.Width + BorderThickness.Left + BorderThickness.Right;
            BorderHeight = reply.Height + BorderThickness.Top + BorderThickness.Bottom;
            BorderColor = new SolidColorBrush(GetRandomColor());

            var rects = Client.GetArrayRectAsync(_token);
            await foreach (var rectangle in rects)//добавляем все прямоугольники
            {
                Rectangles.Add(new RectangleWithColor(rectangle.Width, rectangle.Height,
                                                      rectangle.X, rectangle.Y,
                                                      new SolidColorBrush(GetRandomColor())));
            }
            //обновляем прямоугольники
            while (!_token.IsCancellationRequested)
            {
                rects = Client.GetArrayRectAsync(_token);
                int i = -1;
                await foreach (var rectangle in rects)
                {
                    i++;
                    if (rectangle.Width == 0)
                        continue;

                    //тк при отправке мы не можем отправить null, поэтому мы отправляли пустой прямоугольник
                    //таким образом мы "восстанавливаем" все прямоугольники,
                    //которые были получены, но еще не посчитаны на тот момент
                    if (Rectangles[i] != null && Rectangles[i].Width == 0)
                    {
                        Rectangles[i].Width = rectangle.Width;
                        Rectangles[i].Height = rectangle.Height;
                    }
                    Rectangles[i].X = rectangle.X;
                    Rectangles[i].Y = rectangle.Y;
                }
            }
            _cancel.Set();
        }

        public async void Stop()
        {
            if(!Client?.Stop() ?? false)
                return;

            await Task.Run(() =>
            {
                _cancelTokenSource?.Cancel();
                _cancel?.WaitOne();
            });

            Rectangles.Clear();
        }
        private Color GetRandomColor()
        {
            Random random = new();
            return Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public class RectangleWithColor : INotifyPropertyChanged
        {
            public double Width
            {
                get => _width;
                set
                {
                    _width = value;
                    RaisePropertyChanged(nameof(Width));
                }
            }
            public double Height
            {
                get => _height;
                set
                {
                    _height = value;
                    RaisePropertyChanged(nameof(Height));
                }
            }
            public double X
            {
                get => _x;
                set
                {
                    _x = value;
                    RaisePropertyChanged(nameof(X));
                }
            }
            public double Y
            {
                get => _y;
                set
                {
                    _y = value;
                    RaisePropertyChanged(nameof(Y));
                }
            }
            public SolidColorBrush Color 
            { 
                get => _color;
                set
                {
                    _color = value;
                    RaisePropertyChanged(nameof(Color));
                }
            }
            private double _width, _height, _x, _y;
            private SolidColorBrush _color = new SolidColorBrush();

            public RectangleWithColor(double width, double height, double x, double y, SolidColorBrush color)
            {
                Width = width;
                Height = height;
                X = x;
                Y = y;
                Color = color;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            public void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
