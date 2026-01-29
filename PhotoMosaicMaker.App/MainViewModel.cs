using Microsoft.Win32;
using PhotoMosaicMaker.Core.Engine;
using PhotoMosaicMaker.Core.Imaging;
using PhotoMosaicMaker.Core.Models;
using PhotoMosaicMaker.Core.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoMosaicMaker.App
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        public enum EnOutputResolutionPreset
        {
            Uhd,
            Qhd,
            Fhd,
            Hd
        }

        #region Fields

        private static readonly MosaicSettings MosaicDefaults = new MosaicSettings();
        private static readonly YoutubeDownloadOptions YtDefaults = new YoutubeDownloadOptions();

        private string _targetPath = "";

        private string _outputFolder = "";
        private string _sourcesFolder = "";
        private string _outputFileNameText = "mosaic_out";

        private bool _useSourcePatches = MosaicDefaults.UseSourcePatches;
        private string _tileSizeText = MosaicDefaults.TileSize.ToString();

        private string _extractFpsText = "1";
        private string _extractMaxFramesText = "0";

        private string _videoUrlInputText = "";
        private string _ytDlpPath = "";

        private string _statusText = "";
        private double _progressValue = 0;

        private bool _ytDlpNoPlaylist = YtDefaults.NoPlaylist;
        private string _ytDlpMaxResolutionText = YtDefaults.MaxResolution.ToString();

        private EnOutputResolutionPreset _outputResolutionPreset = EnOutputResolutionPreset.Fhd;

        private ImageSource? _previewImage;

        private readonly MosaicEngine _engine = new MosaicEngine();
        private PatchLibrary? _library;

        private CancellationTokenSource? _cts;
        private bool _isBusy = false;

        private VideoSourceItem? _selectedVideoSource;

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        public MainViewModel()
        {
            VideoSources = new ObservableCollection<VideoSourceItem>();

            BrowseTargetCommand = new RelayCommand(BrowseTarget, () => _isBusy == false);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder, () => _isBusy == false);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, CanOpenOutputFolder);

            AddVideoFilesCommand = new RelayCommand(AddVideoFiles, () => _isBusy == false);
            AddVideoUrlCommand = new RelayCommand(AddVideoUrl, CanAddVideoUrl);
            RemoveSelectedVideoSourceCommand = new RelayCommand(RemoveSelectedVideoSource, CanRemoveSelectedVideoSource);
            ClearVideoSourcesCommand = new RelayCommand(ClearVideoSources, () => _isBusy == false && VideoSources.Count > 0);

            BrowseYtDlpCommand = new RelayCommand(BrowseYtDlp, () => _isBusy == false);

            ExtractFramesCommand = new RelayCommand(ExtractFrames, CanExtractFrames);

            BuildLibraryCommand = new RelayCommand(BuildLibrary, CanBuildLibrary);
            RenderCommand = new RelayCommand(Render, CanRender);
            CancelCommand = new RelayCommand(Cancel, () => _isBusy == true);

            // 기본 출력 폴더 설정 + sources 자동 생성
            SetOutputFolderInternal(GetDefaultOutputFolder());

            RaiseCanExecuteAll();
        }

        

        #region Properties

        // ----- Readonly paths -----
        public string TargetPath
        {
            get => _targetPath;
            private set
            {
                if (_targetPath == value) return;
                _targetPath = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            private set
            {
                if (_outputFolder == value) return;
                _outputFolder = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public string SourcesFolder
        {
            get => _sourcesFolder;
            private set
            {
                if (_sourcesFolder == value) return;
                _sourcesFolder = value;
                OnPropertyChanged();

                _library?.Dispose();
                _library = null;

                RaiseCanExecuteAll();
            }
        }

        // ----- Output file name only -----
        public string OutputFileNameText
        {
            get => _outputFileNameText;
            set
            {
                if (_outputFileNameText == value) return;
                _outputFileNameText = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public bool UseSourcePatches
        {
            get => _useSourcePatches;
            set
            {
                if (_useSourcePatches == value) return;
                _useSourcePatches = value;
                OnPropertyChanged();

                _library?.Dispose();
                _library = null;

                StatusText = "옵션 변경됨: Build Library를 다시 실행하세요.";
                RaiseCanExecuteAll();
            }
        }

        public string TileSizeText
        {
            get => _tileSizeText;
            set
            {
                if (_tileSizeText == value) return;
                _tileSizeText = value;
                OnPropertyChanged();

                _library?.Dispose();
                _library = null;

                RaiseCanExecuteAll();
            }
        }

        public string ExtractFpsText
        {
            get => _extractFpsText;
            set
            {
                if (_extractFpsText == value) return;
                _extractFpsText = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public string ExtractMaxFramesText
        {
            get => _extractMaxFramesText;
            set
            {
                if (_extractMaxFramesText == value) return;
                _extractMaxFramesText = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public string VideoUrlInputText
        {
            get => _videoUrlInputText;
            set
            {
                if (_videoUrlInputText == value) return;
                _videoUrlInputText = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public string YtDlpPath
        {
            get => _ytDlpPath;
            private set
            {
                if (_ytDlpPath == value) return;
                _ytDlpPath = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
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

        public bool YtDlpNoPlaylist
        {
            get => _ytDlpNoPlaylist;
            set
            {
                if (_ytDlpNoPlaylist == value) return;
                _ytDlpNoPlaylist = value;
                OnPropertyChanged();
            }
        }

        public string YtDlpMaxResolutionText
        {
            get => _ytDlpMaxResolutionText;
            set
            {
                if (_ytDlpMaxResolutionText == value) return;
                _ytDlpMaxResolutionText = value;
                OnPropertyChanged();
            }
        }

        private int ParseYtDlpMaxResolution()
        {
            if (int.TryParse(YtDlpMaxResolutionText, out int n) == true && n >= 0)
            {
                return n;
            }
            return 720;
        }

        // ----- Unified sources -----
        public ObservableCollection<VideoSourceItem> VideoSources { get; }

        public VideoSourceItem? SelectedVideoSource
        {
            get => _selectedVideoSource;
            set
            {
                if (_selectedVideoSource == value) return;
                _selectedVideoSource = value;
                OnPropertyChanged();
                RaiseCanExecuteAll();
            }
        }

        public EnOutputResolutionPreset OutputResolutionPreset
        {
            get => _outputResolutionPreset;
            set
            {
                if (_outputResolutionPreset == value) return;
                _outputResolutionPreset = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOutputUhd));
                OnPropertyChanged(nameof(IsOutputQhd));
                OnPropertyChanged(nameof(IsOutputFhd));
                OnPropertyChanged(nameof(SelectedOutputWidth));
                OnPropertyChanged(nameof(SelectedOutputHeight));

                TileSizeText = _outputResolutionPreset switch
                {
                    EnOutputResolutionPreset.Uhd => (MosaicDefaults.TileSize * 2).ToString(),
                    EnOutputResolutionPreset.Qhd => (MosaicDefaults.TileSize * 3 / 2).ToString(),
                    EnOutputResolutionPreset.Fhd => MosaicDefaults.TileSize.ToString(),
                    _ => (MosaicDefaults.TileSize * 2 / 3).ToString()
                };

                OnPropertyChanged(nameof(TileSizeText));
            }
        }

        public bool IsOutputUhd
        {
            get => OutputResolutionPreset == EnOutputResolutionPreset.Uhd;
            set
            {
                if (value == true) OutputResolutionPreset = EnOutputResolutionPreset.Uhd;
            }
        }

        public bool IsOutputQhd
        {
            get => OutputResolutionPreset == EnOutputResolutionPreset.Qhd;
            set
            {
                if (value == true) OutputResolutionPreset = EnOutputResolutionPreset.Qhd;
            }
        }

        public bool IsOutputFhd
        {
            get => OutputResolutionPreset == EnOutputResolutionPreset.Fhd;
            set
            {
                if (value == true) OutputResolutionPreset = EnOutputResolutionPreset.Fhd;
            }
        }

        public bool IsOutputHd
        {
            get => OutputResolutionPreset == EnOutputResolutionPreset.Hd;
            set
            {
                if (value == true) OutputResolutionPreset = EnOutputResolutionPreset.Hd;
            }
        }

        public int SelectedOutputWidth => OutputResolutionPreset switch
        {
            EnOutputResolutionPreset.Uhd => 3840,
            EnOutputResolutionPreset.Qhd => 2560,
            EnOutputResolutionPreset.Fhd => 1920,
            _ => 1280
        };

        public int SelectedOutputHeight => OutputResolutionPreset switch
        {
            EnOutputResolutionPreset.Uhd => 2160,
            EnOutputResolutionPreset.Qhd => 1440,
            EnOutputResolutionPreset.Fhd => 1080,
            _ => 720
        };

        #endregion

        #region commands

        public RelayCommand BrowseTargetCommand { get; }
        public RelayCommand BrowseOutputFolderCommand { get; }
        public RelayCommand OpenOutputFolderCommand { get; }

        public RelayCommand AddVideoFilesCommand { get; }
        public RelayCommand AddVideoUrlCommand { get; }
        public RelayCommand RemoveSelectedVideoSourceCommand { get; }
        public RelayCommand ClearVideoSourcesCommand { get; }

        public RelayCommand BrowseYtDlpCommand { get; }

        public RelayCommand ExtractFramesCommand { get; }

        public RelayCommand BuildLibraryCommand { get; }
        public RelayCommand RenderCommand { get; }
        public RelayCommand CancelCommand { get; }

        #endregion

        #region Methods

        private void BrowseTarget()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select target image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                TargetPath = dlg.FileName;
            }
        }

        private void BrowseOutputFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select output folder",
                Multiselect = false,
                InitialDirectory = Directory.Exists(OutputFolder) == true ? OutputFolder : null
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                SetOutputFolderInternal(dialog.FolderName);
            }
        }

        private bool CanOpenOutputFolder()
        {
            if (_isBusy == true) return false;
            if (Directory.Exists(OutputFolder) == false) return false;
            return true;
        }

        private void OpenOutputFolder()
        {
            if (Directory.Exists(OutputFolder) == false) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = OutputFolder,
                UseShellExecute = true
            });
        }

        private void SetOutputFolderInternal(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) == true) return;

            Directory.CreateDirectory(folder);

            OutputFolder = folder;

            string? parent = Path.GetDirectoryName(OutputFolder);
            string sourcesRoot = string.IsNullOrWhiteSpace(parent) == true ? OutputFolder : parent;

            string sources = Path.Combine(sourcesRoot, "sources");
            Directory.CreateDirectory(sources);
            SourcesFolder = sources;
        }

        private static string GetDefaultOutputFolder()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(root) == true)
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            return Path.Combine(root, "PhotoMosaicMaker", "Output");
        }

        private void AddVideoFiles()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select video files",
                Filter = "Video Files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All Files|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            foreach (var f in dlg.FileNames)
            {
                if (string.IsNullOrWhiteSpace(f) == true) continue;

                bool exists = VideoSources.Any(x => x.Kind == VideoSourceKind.File && string.Equals(x.Value, f, StringComparison.OrdinalIgnoreCase) == true);
                if (exists == false)
                {
                    VideoSources.Add(new VideoSourceItem(VideoSourceKind.File, f));
                }
            }

            if (SelectedVideoSource == null && VideoSources.Count > 0)
            {
                SelectedVideoSource = VideoSources[0];
            }

            RaiseCanExecuteAll();
        }

        private bool CanAddVideoUrl()
        {
            if (_isBusy == true) return false;
            if (string.IsNullOrWhiteSpace(VideoUrlInputText) == true) return false;
            if (string.IsNullOrWhiteSpace(SourcesFolder) == true) return false;
            return true;
        }

        private void AddVideoUrl()
        {
            string url = (VideoUrlInputText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url) == true) return;

            RunAsync(async token =>
            {
                Directory.CreateDirectory(SourcesFolder);

                string cacheFolder = Path.Combine(SourcesFolder, "_video_cache");
                Directory.CreateDirectory(cacheFolder);

                string exe = string.IsNullOrWhiteSpace(YtDlpPath) == true ? "yt-dlp" : YtDlpPath;

                StatusText = "Downloading video from URL...";
                ProgressValue = 0;

                var downloader = new YoutubeVideoDownloader();
                var opt = new YoutubeDownloadOptions
                {
                    DownloaderExePath = exe,
                    NoPlaylist = YtDlpNoPlaylist,
                    DeleteDownloadedVideo = false,
                    MaxResolution = ParseYtDlpMaxResolution()
                };

                string videoPath = await downloader.DownloadAsync(url, cacheFolder, opt, token);

                // URL이 아니라 File 소스로 추가
                var existing = VideoSources.FirstOrDefault(x =>
                    x.Kind == VideoSourceKind.File &&
                    string.Equals(x.Value, videoPath, StringComparison.OrdinalIgnoreCase) == true);

                if (existing == null)
                {
                    existing = new VideoSourceItem(VideoSourceKind.File, videoPath);
                    VideoSources.Add(existing);
                }

                SelectedVideoSource = existing;
                VideoUrlInputText = "";

                StatusText = $"Downloaded & added: {Path.GetFileName(videoPath)}";
                ProgressValue = 1;

                RaiseCanExecuteAll();
            });
        }

        private bool CanRemoveSelectedVideoSource()
        {
            if (_isBusy == true) return false;
            if (SelectedVideoSource == null) return false;
            return true;
        }

        private void RemoveSelectedVideoSource()
        {
            if (SelectedVideoSource == null) return;

            int idx = VideoSources.IndexOf(SelectedVideoSource);
            if (idx >= 0)
            {
                VideoSources.RemoveAt(idx);
            }

            if (VideoSources.Count > 0)
            {
                SelectedVideoSource = VideoSources[0];
            }
            else
            {
                SelectedVideoSource = null;
            }

            RaiseCanExecuteAll();
        }

        private void ClearVideoSources()
        {
            VideoSources.Clear();
            SelectedVideoSource = null;
            RaiseCanExecuteAll();
        }

        private void BrowseYtDlp()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select yt-dlp executable",
                Filter = "Executable|*.exe|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                YtDlpPath = dlg.FileName;
            }
        }

        private bool CanExtractFrames()
        {
            if (_isBusy == true) return false;
            if (VideoSources.Count == 0) return false;
            if (Directory.Exists(SourcesFolder) == false) return false;
            return true;
        }

        private void ExtractFrames()
        {
            RunAsync(async token =>
            {
                if (VideoSources.Count == 0)
                {
                    StatusText = "No video sources.";
                    return;
                }

                Directory.CreateDirectory(SourcesFolder);

                double fps = ParseFps();
                int maxFrames = ParseMaxFrames();

                var frameOpt = new VideoFrameExtractionOptions
                {
                    FramesPerSecond = fps,
                    MaxFramesPerVideo = maxFrames,
                    JpegQuality = 3
                };

                var extractor = new FfmpegFrameExtractor("ffmpeg");
                var downloader = new YoutubeVideoDownloader();

                string exe = string.IsNullOrWhiteSpace(YtDlpPath) == true ? "yt-dlp" : YtDlpPath;
                string cacheFolder = Path.Combine(SourcesFolder, "_video_cache");
                Directory.CreateDirectory(cacheFolder);

                int total = VideoSources.Count;

                StatusText = "Extracting frames...";
                ProgressValue = 0;

                for (int i = 0; i < total; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var src = VideoSources[i];

                    string videoPath = "";
                    bool deleteAfter = false;

                    if (src.Kind == VideoSourceKind.File)
                    {
                        videoPath = src.Value;
                        if (File.Exists(videoPath) == false)
                        {
                            throw new FileNotFoundException("동영상 파일을 찾을 수 없습니다.", videoPath);
                        }
                    }
                    else
                    {
                        StatusText = $"Downloading ({i + 1}/{total})...";
                        ProgressValue = (double)i / total;

                        var opt = new YoutubeDownloadOptions
                        {
                            DownloaderExePath = exe,
                            NoPlaylist = true,
                            DeleteDownloadedVideo = true,
                            MaxResolution = 720,
                        };

                        videoPath = await downloader.DownloadAsync(src.Value, cacheFolder, opt, token);
                        deleteAfter = opt.DeleteDownloadedVideo;
                    }

                    var innerProgress = new Progress<VideoExtractionProgress>(p =>
                    {
                        // 0~1을 전체 소스 진행률로 맵핑 (각 소스는 1개 비디오라고 가정)
                        double baseFrac = (double)i / total;
                        double stepFrac = 1.0 / total;
                        double local = (p.TotalVideos > 0) ? ((double)p.CurrentVideo / p.TotalVideos) : 0;

                        ProgressValue = baseFrac + (stepFrac * local);

                        string label = src.Kind == VideoSourceKind.Url ? "URL" : "File";
                        StatusText = $"{label} ({i + 1}/{total}) {p.Stage}: {Path.GetFileName(p.VideoPath)}";
                    });

                    await extractor.ExtractFramesAsync(new[] { videoPath }, SourcesFolder, frameOpt, innerProgress, token);

                    if (deleteAfter == true)
                    {
                        try { File.Delete(videoPath); } catch { /* ignore */ }
                    }
                }

                _library?.Dispose();
                _library = null;

                StatusText = "Deduplicating similar frames...";
                ProgressValue = 0.98;

                var dedup = ImageDeduplicator.DedupFolderByDHash(
                    SourcesFolder,
                    hammingThreshold: 6,           // 필요하면 8로 올리면 더 많이 제거
                    moveToDuplicatesFolder: false,  // 안전: 삭제 대신 _duplicates로 이동
                    cancellationToken: token);

                StatusText = $"Frames extracted. Dedup moved: {dedup.MovedToDuplicates} / {dedup.Total}. (Build Library 실행)";
                ProgressValue = 1;

                RaiseCanExecuteAll();
            });
        }

        private bool CanBuildLibrary()
        {
            if (_isBusy == true) return false;
            if (Directory.Exists(SourcesFolder) == false) return false;
            return EnumerateSourceImages(SourcesFolder).Count > 0;
        }

        private void BuildLibrary()
        {
            RunAsync(async token =>
            {
                if (Directory.Exists(SourcesFolder) == false)
                {
                    StatusText = "Sources folder not set.";
                    return;
                }

                var files = EnumerateSourceImages(SourcesFolder);
                if (files.Count == 0)
                {
                    StatusText = "No source images found.";
                    return;
                }

                StatusText = "Building patch library...";
                ProgressValue = 0;

                int tileSize = ParseTileSize();

                var (outW, outH) = ComputeOutputSizeFromTarget(TargetPath, SelectedOutputWidth, SelectedOutputHeight);
                var settings = CreateSettings(tileSize, outW, outH);

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

                RaiseCanExecuteAll();
            });
        }

        private bool CanRender()
        {
            if (_isBusy == true) return false;
            if (_library == null) return false;
            if (File.Exists(TargetPath) == false) return false;
            if (Directory.Exists(OutputFolder) == false) return false;
            if (string.IsNullOrWhiteSpace(OutputFileNameText) == true) return false;
            return true;
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
                    StatusText = "Target image not set.";
                    return;
                }

                Directory.CreateDirectory(OutputFolder);

                string outputPath = BuildUniqueOutputPath(OutputFolder, OutputFileNameText);

                int tileSize = ParseTileSize();
                var (outW, outH) = ComputeOutputSizeFromTarget(TargetPath, SelectedOutputWidth, SelectedOutputHeight);
                var settings = CreateSettings(tileSize, outW, outH);

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
                    SaveByExtension(result, outputPath);

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

                    StatusText = $"Done: {Path.GetFileName(outputPath)}";
                    ProgressValue = 1;
                }
                finally
                {
                    result.Dispose();
                }
            });
        }

        private static void SaveByExtension(Image<Rgba32> img, string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                img.SaveAsJpeg(path);
                return;
            }

            img.SaveAsPng(path);
        }

        private static string BuildUniqueOutputPath(string folder, string fileNameInput)
        {
            string cleaned = (fileNameInput ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cleaned) == true)
            {
                cleaned = "mosaic_out.png";
            }

            cleaned = Path.GetFileName(cleaned);

            if (Path.HasExtension(cleaned) == false)
            {
                cleaned = cleaned + ".png";
            }

            cleaned = SanitizeFileName(cleaned);

            string baseName = Path.GetFileNameWithoutExtension(cleaned);
            string ext = Path.GetExtension(cleaned);

            string candidate = Path.Combine(folder, cleaned);
            if (File.Exists(candidate) == false)
            {
                return candidate;
            }

            for (int i = 1; i < 10000; i++)
            {
                string withNum = $"{baseName} ({i}){ext}";
                string p = Path.Combine(folder, withNum);
                if (File.Exists(p) == false)
                {
                    return p;
                }
            }

            return Path.Combine(folder, $"{baseName}_{Guid.NewGuid():N}{ext}");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private void Cancel()
        {
            _cts?.Cancel();
        }

        private void RunAsync(Func<CancellationToken, Task> work)
        {
            if (_isBusy == true) return;

            _isBusy = true;
            RaiseCanExecuteAll();

            _cts = new CancellationTokenSource();
            _ = RunAsyncInternal(work, _cts);
        }

        private async Task RunAsyncInternal(Func<CancellationToken, Task> work, CancellationTokenSource cts)
        {
            try
            {
                await work(cts.Token);
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
                cts.Dispose();
                if (ReferenceEquals(_cts, cts) == true)
                {
                    _cts = null;
                }

                _isBusy = false;
                RaiseCanExecuteAll();
            }
        }

        private void RaiseCanExecuteAll()
        {
            BrowseTargetCommand?.RaiseCanExecuteChanged();
            BrowseOutputFolderCommand?.RaiseCanExecuteChanged();
            OpenOutputFolderCommand?.RaiseCanExecuteChanged();

            AddVideoFilesCommand?.RaiseCanExecuteChanged();
            AddVideoUrlCommand?.RaiseCanExecuteChanged();
            RemoveSelectedVideoSourceCommand?.RaiseCanExecuteChanged();
            ClearVideoSourcesCommand?.RaiseCanExecuteChanged();

            BrowseYtDlpCommand?.RaiseCanExecuteChanged();

            ExtractFramesCommand?.RaiseCanExecuteChanged();

            BuildLibraryCommand?.RaiseCanExecuteChanged();
            RenderCommand?.RaiseCanExecuteChanged();
            CancelCommand?.RaiseCanExecuteChanged();
        }

        private static List<string> EnumerateSourceImages(string folder)
        {
            if (Directory.Exists(folder) == false) return new List<string>();

            return Directory.EnumerateFiles(folder)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private int ParseTileSize()
        {
            if (int.TryParse(TileSizeText, out int n) == true && n > 0) return n;
            return MosaicDefaults.TileSize;
        }

        private double ParseFps()
        {
            if (double.TryParse(ExtractFpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) == true && n > 0) return n;
            if (double.TryParse(ExtractFpsText, out n) == true && n > 0) return n;
            return 1.0;
        }

        private int ParseMaxFrames()
        {
            if (int.TryParse(ExtractMaxFramesText, out int n) == true && n >= 0) return n;
            return 0;
        }

        private MosaicSettings CreateSettings(int tileSize, int outW, int outH)
        {
            return new MosaicSettings
            {
                OutputWidth = outW,
                OutputHeight = outH,
                TileSize = tileSize,
                MaxPatchReuse = 5,
                ColorAdjustStrength = 0.35f,
                UseSourcePatches = UseSourcePatches
            };
        }

        private static (int W, int H) ComputeOutputSizeFromTarget(string targetPath, int maxW, int maxH)
        {
            var info = Image.Identify(targetPath);
            if (info == null || info.Width <= 0 || info.Height <= 0)
            {
                return (maxW, maxH);
            }

            double scaleW = (double)maxW / info.Width;
            double scaleH = (double)maxH / info.Height;
            double scale = Math.Min(scaleW, scaleH);

            int w = (int)Math.Round(info.Width * scale);
            int h = (int)Math.Round(info.Height * scale);

            if (w < 1) w = 1;
            if (h < 1) h = 1;

            return (w, h);
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
