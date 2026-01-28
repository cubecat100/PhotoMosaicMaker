using PhotoMosaicMaker.Core.Engine;
using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoMosaicMaker.App
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _targetPath = "";
        private string _sourcesFolder = "";
        private string _outputPath = "";
        private string _tileSizeText = "24";
        private string _statusText = "";
        private double _progressValue = 0;

        private ImageSource? _previewImage;

        private readonly MosaicEngine _engine = new MosaicEngine();
        private PatchLibrary? _library;
        private CancellationTokenSource? _cts;
        private bool _isBusy = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel()
        {
            BuildLibraryCommand = new RelayCommand(BuildLibrary, () => _isBusy == false);
            RenderCommand = new RelayCommand(Render, () => _isBusy == false);
            CancelCommand = new RelayCommand(Cancel, () => _isBusy == true);
        }

        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }

        public string SourcesFolder
        {
            get => _sourcesFolder;
            set { _sourcesFolder = value; OnPropertyChanged(); }
        }

        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(); }
        }

        public string TileSizeText
        {
            get => _tileSizeText;
            set { _tileSizeText = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public double ProgressValue
        {
            get => _progressValue;
            private set { _progressValue = value; OnPropertyChanged(); }
        }

        public ImageSource? PreviewImage
        {
            get => _previewImage;
            private set { _previewImage = value; OnPropertyChanged(); }
        }

        public RelayCommand BuildLibraryCommand { get; }
        public RelayCommand RenderCommand { get; }
        public RelayCommand CancelCommand { get; }

        private void BuildLibrary()
        {
            RunAsync(async token =>
            {
                StatusText = "Building patch library...";
                ProgressValue = 0;

                int tileSize = ParseTileSize();

                var files = EnumerateSourceImages(SourcesFolder);
                if (files.Count == 0)
                {
                    StatusText = "No source images found.";
                    return;
                }

                var settings = CreateSettings(tileSize);

                var progress = new Progress<MosaicProgress>(p =>
                {
                    if (p.Total > 0)
                    {
                        ProgressValue = (double)p.Current / p.Total;
                        StatusText = $"{p.Stage}: {p.Current}/{p.Total}";
                    }
                    else
                    {
                        StatusText = $"{p.Stage}";
                    }
                });

                _library?.Dispose();
                _library = null;

                await Task.Run(() =>
                {
                    _library = _engine.BuildPatchLibrary(files, settings, progress, token);
                }, token);

                StatusText = $"Library ready. Patches: {_library!.Patches.Count}";
                ProgressValue = 1;
            });
        }

        private void Render()
        {
            RunAsync(async token =>
            {
                if (_library == null)
                {
                    StatusText = "Build library first.";
                    return;
                }

                if (File.Exists(TargetPath) == false)
                {
                    StatusText = "Target image path invalid.";
                    return;
                }

                int tileSize = ParseTileSize();
                var settings = CreateSettings(tileSize);

                var progress = new Progress<MosaicProgress>(p =>
                {
                    if (p.Total > 0)
                    {
                        ProgressValue = (double)p.Current / p.Total;
                        StatusText = $"{p.Stage}: {p.Current}/{p.Total}";
                    }
                    else
                    {
                        StatusText = $"{p.Stage}";
                    }
                });

                StatusText = "Rendering...";
                ProgressValue = 0;

                Image<Rgba32> result = await Task.Run(() =>
                {
                    return _engine.Render(TargetPath, _library, settings, progress, token);
                }, token);

                try
                {
                    // Save
                    string outPath = OutputPath;
                    if (string.IsNullOrWhiteSpace(outPath) == true)
                    {
                        outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "mosaic_out.png");
                        OutputPath = outPath;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
                    result.Save(outPath);

                    // Preview in UI (convert to BitmapImage)
                    using var ms = new MemoryStream();
                    result.SaveAsPng(ms);
                    ms.Position = 0;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    PreviewImage = bmp;

                    StatusText = $"Done: {outPath}";
                    ProgressValue = 1;
                }
                finally
                {
                    result.Dispose();
                }
            });
        }

        private void Cancel()
        {
            _cts?.Cancel();
        }

        private void RunAsync(Func<CancellationToken, Task> work)
        {
            if (_isBusy == true)
            {
                return;
            }

            _isBusy = true;
            BuildLibraryCommand.RaiseCanExecuteChanged();
            RenderCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();

            _cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await work(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    StatusText = "Canceled.";
                }
                catch (Exception ex)
                {
                    StatusText = ex.Message;
                }
                finally
                {
                    _cts.Dispose();
                    _cts = null;

                    _isBusy = false;
                    BuildLibraryCommand.RaiseCanExecuteChanged();
                    RenderCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            });
        }

        private static List<string> EnumerateSourceImages(string folder)
        {
            if (Directory.Exists(folder) == false)
            {
                return new List<string>();
            }

            return Directory.EnumerateFiles(folder)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private int ParseTileSize()
        {
            if (int.TryParse(TileSizeText, out int n) == true && n > 0)
            {
                return n;
            }
            return 24;
        }

        private static MosaicSettings CreateSettings(int tileSize)
        {
            return new MosaicSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080,
                TileSize = tileSize,
                MaxPatchReuse = 5,
                ColorAdjustStrength = 0.35f
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
