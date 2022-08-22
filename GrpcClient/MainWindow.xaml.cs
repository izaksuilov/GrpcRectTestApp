using System.Threading.Tasks;
using Grpc.Net.Client;
using System;
using System.Windows;
using Google.Protobuf.WellKnownTypes;
using System.Windows.Media;
using System.Windows.Controls;
using System.Threading;
using System.Collections.Generic;

namespace GrpcClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Client Client;
        private CancellationTokenSource? _cancelTokenSource;
        private CancellationToken _token;
        private AutoResetEvent _cancel;
        public MainWindow()
        {
            InitializeComponent();
            Client = new Client();
            Client.Connect();
            
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!Client.Start())
                return;

            _cancelTokenSource = new CancellationTokenSource();
            _token = _cancelTokenSource.Token;
            _cancel = new AutoResetEvent(false);

            var reply = Client.GetFieldSize();
            CanvasBorder.Width = reply.Width + CanvasBorder.BorderThickness.Left + CanvasBorder.BorderThickness.Right;
            CanvasBorder.Height = reply.Height + CanvasBorder.BorderThickness.Top + CanvasBorder.BorderThickness.Bottom;
            
            CanvasBorder.BorderBrush = new SolidColorBrush(GetRandomColor());

            var rects = Client.GetArrayRectAsync(_token);
            await foreach (var rectangle in rects)//добавляем все прямоугольники
            {
                System.Windows.Shapes.Rectangle rect = new()
                {
                    Width = rectangle.Width,
                    Height = rectangle.Height,
                    Stroke = new SolidColorBrush(GetRandomColor())
                };
                Canvas.SetLeft(rect, rectangle.X);
                Canvas.SetTop(rect, rectangle.Y);
                RectsCanvas.Children.Add(rect);
            }
            while (!_token.IsCancellationRequested)
            {
                rects = Client.GetArrayRectAsync(_token);
                int i = -1;
                await foreach (var rectangle in rects)
                {
                    i++;
                    if (rectangle.Width == 0)
                        continue;
                    var rect = RectsCanvas.Children[i] as System.Windows.Shapes.Rectangle;
                    if (rect != null && rect.Width == 0)
                    {
                        rect.Width = rectangle.Width;
                        rect.Height = rectangle.Height;
                    }
                        
                    Canvas.SetLeft(RectsCanvas.Children[i], rectangle.X);
                    Canvas.SetTop(RectsCanvas.Children[i], rectangle.Y);
                }
            }
            _cancel.Set();
        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (!Client?.Stop() ?? false)
                return;

            await Task.Run(() => 
            {
                _cancelTokenSource?.Cancel();
                _cancel?.WaitOne();
            });
            RectsCanvas.Children.Clear();
        }

        private Color GetRandomColor()
        {
            Random random = new();
            return Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        }

    }
}
