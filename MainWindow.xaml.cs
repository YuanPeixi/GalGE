using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GalEngineSample
{
    public partial class MainWindow : Window
    {
        private StoryEngine _engine;
        private MediaPlayer _musicPlayer = new MediaPlayer();

        // 打字机效果控制
        private CancellationTokenSource? _typeCts;

        public MainWindow()
        {
            InitializeComponent();

            _engine = new StoryEngine();
            _engine.NodeChanged += OnNodeChanged;
            _engine.StoryEnded += () =>
            {
                MessageBox.Show("故事结束");
                Close();
            };

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "story.xml");
            _engine.LoadFromFile(path);
            _engine.Start();
        }

        private void OnNodeChanged(StoryNode node)
        {
            // 取消上一次的打字机显示（如果在进行中）
            _typeCts?.Cancel();
            _typeCts = new CancellationTokenSource();

            // 背景
            if (node.BackgroundExplicitlySet)
            {
                if (!string.IsNullOrEmpty(node.Background))
                {
                    var bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, node.Background);
                    if (File.Exists(bgPath))
                    {
                        BackgroundImage.Source = new BitmapImage(new Uri(bgPath));
                    }
                    else
                    {
                        BackgroundImage.Source = null;
                    }
                }
                else
                {
                    // 显式设置为空（清除背景）
                    BackgroundImage.Source = null;
                }
            }
            // 如果没有显式设置背景，将维持当前背景（继承逻辑已在加载时处理）

            // 音乐（如果指定则播放；若空字符串表示停止）
            if (node.MusicExplicitlySet)
            {
                if (!string.IsNullOrEmpty(node.Music))
                {
                    var musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, node.Music);
                    if (File.Exists(musicPath))
                    {
                        _musicPlayer.Open(new Uri(musicPath));
                        _musicPlayer.Volume = 0.6;
                        _musicPlayer.Play();
                    }
                }
                else
                {
                    _musicPlayer.Stop();
                }
            }

            // 角色
            CharacterCanvas.Children.Clear();
            foreach (var c in node.Characters)
            {
                if (string.IsNullOrEmpty(c.Image)) continue;
                var imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, c.Image);
                if (!File.Exists(imgPath)) continue;

                var img = new Image
                {
                    Source = new BitmapImage(new Uri(imgPath)),
                    Width = 420,
                    Height = 600,
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0
                };

                // 简单的左右布局
                double left = c.Side == "left" ? 40 : (this.Width - 460);
                Canvas.SetLeft(img, left);
                Canvas.SetTop(img, 80);
                CharacterCanvas.Children.Add(img);

                // 简单的淡入动画
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                img.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            // 说话者（继承逻辑已处理）
            SpeakerText.Text = node.Speaker ?? "";

            // 文本：使用逐字显示（可被中断）
            DialogueText.Text = "";
            _ = TypeTextAsync(node.Text ?? "", _typeCts.Token);

            // 选项：更新 ItemsControl
            OptionsPanel.Items.Clear();
            if (node.Options.Any())
            {
                NextButton.Visibility = Visibility.Collapsed;

                foreach (var opt in node.Options)
                {
                    var btn = new Button
                    {
                        Content = opt.Text,
                        Margin = new Thickness(6, 4, 6, 4),
                        MinWidth = 220,
                        MinHeight = 36,
                    };

                    btn.Click += (s, e) =>
                    {
                        // 当玩家选择选项，取消打字机（如果正在进行），并直接应用选项
                        _typeCts?.Cancel();
                        _engine.ApplyOption(opt);
                    };

                    OptionsPanel.Items.Add(btn);
                }
            }
            else
            {
                OptionsPanel.Items.Clear();
                NextButton.Visibility = Visibility.Visible;
            }
        }

        private async Task TypeTextAsync(string text, CancellationToken ct)
        {
            try
            {
                // 简单的打字机效果：每个字符 8-15ms（快速），遇到中文延长一点
                for (int i = 0; i < text.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    DialogueText.Text += text[i];
                    int delay = (text[i] > 0x3000 && text[i] < 0x9FFF) ? 28 : 12;
                    await Task.Delay(delay, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 中断：立即显示完整文本
                DialogueText.Text = text;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _typeCts?.Cancel();
            _engine.Next();
        }
    }
}