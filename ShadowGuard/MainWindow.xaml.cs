using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace ShadowGuard;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ProjectScanner _scanner = new();
    private readonly RiskScoringService _riskScoringService = new();
    private readonly GateDecisionService _gateDecisionService = new();
    private readonly PluginService _pluginService = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ScanResult? _currentResult;
    private string _targetPath = string.Empty;
    private string _findingsSearchText = string.Empty;
    private string _componentsSearchText = string.Empty;
    private string _statusMessage = "请先选择项目并开始扫描。";
    private string _selectedProjectName = "未选择项目";
    private string _lastScanAt = "尚未扫描";
    private int _totalDependencies;
    private int _directDependencies;
    private int _transitiveDependencies;
    private int _totalFindings;
    private int _criticalFindings;
    private int _highFindings;
    private int _overallScore;
    private string _overallSeverity = "无";
    private string _gateOutcomeText = "待执行";
    private string _gateReasonText = "请先执行一次扫描以生成闸门结论。";
    private int _blockThreshold = 70;
    private bool _blockOnMalicious = true;
    private bool _blockOnLicenseRisk = true;
    private bool _warnOnUnknownSource = true;
    private string _sbomPreview = "{\n  \"message\": \"请先执行扫描以生成 SBOM 预览。\"\n}";
    private readonly string _pluginDirectory;
    private readonly string _sampleProjectPath;

    public MainWindow()
    {
        FindingsView = CollectionViewSource.GetDefaultView(Findings);
        FindingsView.Filter = FilterFindings;

        ComponentsView = CollectionViewSource.GetDefaultView(Components);
        ComponentsView.Filter = FilterComponents;

        _pluginDirectory = WorkspaceLocator.ResolveOrCreateDirectory("plugins");
        _sampleProjectPath = WorkspaceLocator.ResolvePath("samples", "demo-workspace");
        _targetPath = Directory.Exists(_sampleProjectPath) ? _sampleProjectPath : string.Empty;

        InitializeComponent();
        DataContext = this;
        LoadPlugins();
    }

    public ObservableCollection<Finding> Findings { get; } = new();
    public ObservableCollection<DependencyComponent> Components { get; } = new();
    public ObservableCollection<PluginDefinition> Plugins { get; } = new();
    public ObservableCollection<Finding> TopFindings { get; } = new();
    public ObservableCollection<string> GateTriggers { get; } = new();

    public ICollectionView FindingsView { get; }
    public ICollectionView ComponentsView { get; }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    public string FindingsSearchText
    {
        get => _findingsSearchText;
        set
        {
            if (SetProperty(ref _findingsSearchText, value))
            {
                FindingsView.Refresh();
            }
        }
    }

    public string ComponentsSearchText
    {
        get => _componentsSearchText;
        set
        {
            if (SetProperty(ref _componentsSearchText, value))
            {
                ComponentsView.Refresh();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedProjectName
    {
        get => _selectedProjectName;
        set => SetProperty(ref _selectedProjectName, value);
    }

    public string LastScanAt
    {
        get => _lastScanAt;
        set => SetProperty(ref _lastScanAt, value);
    }

    public int TotalDependencies
    {
        get => _totalDependencies;
        set => SetProperty(ref _totalDependencies, value);
    }

    public int DirectDependencies
    {
        get => _directDependencies;
        set => SetProperty(ref _directDependencies, value);
    }

    public int TransitiveDependencies
    {
        get => _transitiveDependencies;
        set => SetProperty(ref _transitiveDependencies, value);
    }

    public int TotalFindings
    {
        get => _totalFindings;
        set => SetProperty(ref _totalFindings, value);
    }

    public int CriticalFindings
    {
        get => _criticalFindings;
        set
        {
            if (SetProperty(ref _criticalFindings, value))
            {
                OnPropertyChanged(nameof(CriticalHighSummary));
            }
        }
    }

    public int HighFindings
    {
        get => _highFindings;
        set
        {
            if (SetProperty(ref _highFindings, value))
            {
                OnPropertyChanged(nameof(CriticalHighSummary));
            }
        }
    }

    public int OverallScore
    {
        get => _overallScore;
        set => SetProperty(ref _overallScore, value);
    }

    public string OverallSeverity
    {
        get => _overallSeverity;
        set => SetProperty(ref _overallSeverity, value);
    }

    public string GateOutcomeText
    {
        get => _gateOutcomeText;
        set => SetProperty(ref _gateOutcomeText, value);
    }

    public string GateReasonText
    {
        get => _gateReasonText;
        set => SetProperty(ref _gateReasonText, value);
    }

    public int BlockThreshold
    {
        get => _blockThreshold;
        set => SetProperty(ref _blockThreshold, value);
    }

    public bool BlockOnMalicious
    {
        get => _blockOnMalicious;
        set => SetProperty(ref _blockOnMalicious, value);
    }

    public bool BlockOnLicenseRisk
    {
        get => _blockOnLicenseRisk;
        set => SetProperty(ref _blockOnLicenseRisk, value);
    }

    public bool WarnOnUnknownSource
    {
        get => _warnOnUnknownSource;
        set => SetProperty(ref _warnOnUnknownSource, value);
    }

    public string SbomPreview
    {
        get => _sbomPreview;
        set => SetProperty(ref _sbomPreview, value);
    }

    public string CriticalHighSummary => $"严重：{CriticalFindings} | 高危：{HighFindings}";
    public int EnabledPluginCount => Plugins.Count(plugin => plugin.Enabled);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool FilterFindings(object item)
    {
        if (item is not Finding finding)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FindingsSearchText))
        {
            return true;
        }

        var query = FindingsSearchText.Trim();
        return finding.DependencyName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || finding.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
            || finding.CategoryText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || finding.SeverityText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || finding.Recommendation.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterComponents(object item)
    {
        if (item is not DependencyComponent component)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ComponentsSearchText))
        {
            return true;
        }

        var query = ComponentsSearchText.Trim();
        return component.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.Ecosystem.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.SourceType.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.SourceTypeText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.DependencyTypeText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.SeverityText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || component.EvidenceFilesDisplay.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TargetPath) || !Directory.Exists(TargetPath))
        {
            System.Windows.MessageBox.Show("请先选择一个有效的项目目录再执行扫描。", "ShadowGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusMessage = "正在扫描项目依赖，请稍候...";
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        try
        {
            var policy = new ScanPolicy
            {
                BlockScoreThreshold = BlockThreshold,
                BlockOnMalicious = BlockOnMalicious,
                BlockOnLicenseRisk = BlockOnLicenseRisk,
                WarnOnUnknownSource = WarnOnUnknownSource
            };

            var pluginSnapshot = Plugins.Select(plugin => plugin.Clone()).ToList();
            var result = await Task.Run(() =>
            {
                var components = _scanner.DiscoverComponents(TargetPath);
                var scanResult = _riskScoringService.BuildResult(TargetPath, components, pluginSnapshot);
                scanResult.GateDecision = _gateDecisionService.Evaluate(scanResult, policy);
                return scanResult;
            });

            _currentResult = result;
            ApplyResult(result);
            StatusMessage = $"扫描完成，共分析 {result.Summary.TotalDependencies} 个依赖组件。";
        }
        catch (Exception exception)
        {
            StatusMessage = "扫描失败，请检查项目目录或规则配置。";
            System.Windows.MessageBox.Show(exception.Message, "ShadowGuard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "请选择需要使用 ShadowGuard 扫描的项目目录。",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(TargetPath) ? TargetPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            TargetPath = dialog.SelectedPath;
            StatusMessage = "项目目录已更新，可开始新的扫描。";
        }
    }

    private void UseSample_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_sampleProjectPath))
        {
            TargetPath = _sampleProjectPath;
            StatusMessage = "已加载内置多生态示例项目，可直接体验完整扫描流程。";
        }
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null)
        {
            System.Windows.MessageBox.Show("请先执行扫描，再导出检测报告。", "ShadowGuard", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ExportPayload(_currentResult, "导出 ShadowGuard 检测报告", $"shadowguard-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    }

    private void ExportSbom_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null)
        {
            System.Windows.MessageBox.Show("请先执行扫描，再导出 SBOM 文件。", "ShadowGuard", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ExportPayload(_currentResult.Sbom, "导出 ShadowGuard SBOM", $"shadowguard-sbom-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    }

    private void ReloadPlugins_Click(object sender, RoutedEventArgs e)
    {
        LoadPlugins();
        StatusMessage = "插件规则已重新加载，请重新扫描以应用最新规则。";
    }

    private void OpenPluginFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _pluginDirectory,
            UseShellExecute = true
        });
    }

    private void ExportPayload(object payload, string title, string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        File.WriteAllText(dialog.FileName, json);
        StatusMessage = $"已导出文件：{Path.GetFileName(dialog.FileName)}";
    }

    private void LoadPlugins()
    {
        foreach (var plugin in Plugins)
        {
            plugin.PropertyChanged -= Plugin_PropertyChanged;
        }

        Plugins.Clear();
        foreach (var plugin in _pluginService.LoadPlugins(_pluginDirectory))
        {
            plugin.PropertyChanged += Plugin_PropertyChanged;
            Plugins.Add(plugin);
        }

        OnPropertyChanged(nameof(EnabledPluginCount));
    }

    private void Plugin_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PluginDefinition.Enabled))
        {
            OnPropertyChanged(nameof(EnabledPluginCount));
            StatusMessage = "插件启用状态已变更，请重新扫描以应用新规则。";
        }
    }

    private void ApplyResult(ScanResult result)
    {
        ReplaceItems(Findings, result.Findings);
        ReplaceItems(Components, result.Components);
        ReplaceItems(TopFindings, result.Findings.Take(5).ToList());
        ReplaceItems(GateTriggers, result.GateDecision.TriggeredPolicies);

        SelectedProjectName = new DirectoryInfo(result.TargetPath).Name;
        LastScanAt = result.ScannedAt.ToString("yyyy-MM-dd HH:mm:ss");
        TotalDependencies = result.Summary.TotalDependencies;
        DirectDependencies = result.Summary.DirectDependencies;
        TransitiveDependencies = result.Summary.TransitiveDependencies;
        TotalFindings = result.Summary.FindingsCount;
        CriticalFindings = result.Summary.CriticalCount;
        HighFindings = result.Summary.HighCount;
        OverallScore = result.Summary.OverallScore;
        OverallSeverity = LocalizationHelper.ToChineseSeverity(result.Summary.OverallSeverity);
        GateOutcomeText = LocalizationHelper.ToChineseGateOutcome(result.GateDecision.Outcome);
        GateReasonText = result.GateDecision.Reason;
        SbomPreview = JsonSerializer.Serialize(result.Sbom, _jsonOptions);

        FindingsView.Refresh();
        ComponentsView.Refresh();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


