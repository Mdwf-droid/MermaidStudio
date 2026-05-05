using System.IO;
using System.Net;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MermaidStudio.Application.Editing;
using MermaidStudio.Application.Export;
using MermaidStudio.Application.Import;
using MermaidStudio.Application.Persistence;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.States;
using MermaidStudio.UI.Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Editing;
using DocumentFlowDirection = MermaidStudio.Domain.Diagrams.FlowDirection;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private enum MermaidImportKind
    {
        Flowchart,
        StateDiagram
    }

    private readonly CommandHistory _history = new();
    private readonly FlowchartExportService _flowchartExportService = new();
    private readonly StateDiagramExportService _stateDiagramExportService = new();

    private readonly SelectionService _selectionService = new();
    private readonly DiagramEditingService _diagramEditingService;
    private readonly InspectorStateService _inspectorStateService;
    private readonly DiagramDocumentService _documentService = new();

    private readonly DiagramDocumentJsonService _jsonService = new();
    private readonly FlowchartMermaidImportService _flowchartImportService = new();
    private readonly StateDiagramMermaidImportService _stateDiagramImportService = new();

    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    private bool _uiReady;
    private bool _suspendFlowDirectionHandling;
    private bool _suspendDiagramKindHandling;

    public MainWindow()
    {
        _diagramEditingService = new DiagramEditingService(_selectionService);
        _inspectorStateService = new InspectorStateService(_selectionService, _documentService);

        AvaloniaXamlLoader.Load(this);

        GetWorkspace().Configure(_selectionService, _diagramEditingService, _documentService, _history);
        GetWorkspace().WorkspaceStateChanged += OnWorkspaceStateChanged;

        GetInspector().ApplyNodeLabelRequested += OnApplyNodeLabelRequested;
        GetInspector().ApplyNodeStyleRequested += OnApplyNodeStyleRequested;
        GetInspector().ApplyEdgeLabelRequested += OnApplyEdgeLabelRequested;
        GetInspector().ApplyEdgeStyleRequested += OnApplyEdgeStyleRequested;

        _uiReady = true;

        SyncWorkspaceDocumentIfNeeded();
        ApplyDiagramKindToUi();
        RefreshInspector();
    }

    private DiagramWorkspaceControl GetWorkspace()
        => this.FindControl<DiagramWorkspaceControl>("WorkspaceControl")
           ?? throw new InvalidOperationException("WorkspaceControl introuvable dans MainWindow.");

    private InspectorPaneControl GetInspector()
        => this.FindControl<InspectorPaneControl>("InspectorPane")
           ?? throw new InvalidOperationException("InspectorPane introuvable dans MainWindow.");

    private ComboBox GetDiagramKindComboBox()
        => this.FindControl<ComboBox>("DiagramKindComboBox")
           ?? throw new InvalidOperationException("DiagramKindComboBox introuvable dans MainWindow.");

    private ComboBox GetFlowDirectionComboBox()
        => this.FindControl<ComboBox>("FlowDirectionComboBox")
           ?? throw new InvalidOperationException("FlowDirectionComboBox introuvable dans MainWindow.");

    private Button GetAddStartStateButton()
        => this.FindControl<Button>("AddStartStateButton")
           ?? throw new InvalidOperationException("AddStartStateButton introuvable dans MainWindow.");

    private Button GetAddEndStateButton()
        => this.FindControl<Button>("AddEndStateButton")
           ?? throw new InvalidOperationException("AddEndStateButton introuvable dans MainWindow.");

    private DiagramKind CurrentDiagramKind => _documentService.CurrentDocument.Kind;

    private void OnWorkspaceStateChanged(object? sender, EventArgs e)
    {
        RefreshInspector();
    }

    // =========================================================
    // Toolbar / kind / viewport / special buttons
    // =========================================================
    private void OnDiagramKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suspendDiagramKindHandling || !_uiReady)
            return;

        var selectedKind = GetDiagramKindComboBox().SelectedIndex == 1
            ? DiagramKind.StateDiagram
            : DiagramKind.Flowchart;

        if (selectedKind == CurrentDiagramKind)
            return;

        GetWorkspace().ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        _history.Clear();

        _documentService.ResetToKind(selectedKind);

        if (selectedKind == DiagramKind.Flowchart)
            _currentDiagramFlowDirection = DiagramFlowDirection.LR;

        ApplyDiagramKindToUi();
        ApplyDocumentDirectionToUi();
        GetWorkspace().LoadCurrentDocument(_currentDiagramFlowDirection);
        RefreshInspector();
        Focus();
    }

    private void ApplyDiagramKindToUi()
    {
        if (!_uiReady)
            return;

        _suspendDiagramKindHandling = true;
        GetDiagramKindComboBox().SelectedIndex = CurrentDiagramKind == DiagramKind.StateDiagram ? 1 : 0;
        _suspendDiagramKindHandling = false;

        var isState = CurrentDiagramKind == DiagramKind.StateDiagram;
        GetAddStartStateButton().IsEnabled = isState;
        GetAddEndStateButton().IsEnabled = isState;
        GetFlowDirectionComboBox().IsEnabled = !isState;
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
        => GetWorkspace().ZoomIn();

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
        => GetWorkspace().ZoomOut();

    private void OnResetZoomClicked(object? sender, RoutedEventArgs e)
        => GetWorkspace().ResetZoom();

    private void OnFitClicked(object? sender, RoutedEventArgs e)
        => GetWorkspace().FitToContent();

    private void OnCenterClicked(object? sender, RoutedEventArgs e)
        => GetWorkspace().CenterOnContent();

    private void OnAddStartStateClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.StateDiagram)
            return;

        GetWorkspace().CreateStateNode(
            StateNodeKind.Start,
            80,
            80 + _documentService.CurrentDocument.StateNodes.Count * 55);
    }

    private void OnAddEndStateClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.StateDiagram)
            return;

        GetWorkspace().CreateStateNode(
            StateNodeKind.End,
            160,
            80 + _documentService.CurrentDocument.StateNodes.Count * 55);
    }

    // =========================================================
    // Save / Load JSON
    // =========================================================
    private async void OnSaveJsonClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram as JSON",
            SuggestedFileName = "diagram.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file == null)
            return;

        var json = _jsonService.Serialize(_documentService.CurrentDocument);

        await using var stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private async void OnLoadJsonClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load diagram JSON",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var document = _jsonService.Deserialize(json);
            LoadDocumentIntoShell(document);
        }
        catch (Exception ex)
        {
            GetInspector().MermaidOutputText = $"Load JSON failed: {ex.Message}";
        }
    }

    // =========================================================
    // Import Mermaid
    // =========================================================
    private async void OnImportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new ImportMermaidWindow();
        var mermaidText = await dialog.ShowDialog<string?>(this);

        if (string.IsNullOrWhiteSpace(mermaidText))
            return;

        try
        {
            var importKind = DetectMermaidImportKind(mermaidText);

            var document = importKind switch
            {
                MermaidImportKind.Flowchart => _flowchartImportService.Import(mermaidText),
                MermaidImportKind.StateDiagram => _stateDiagramImportService.Import(mermaidText),
                _ => throw new InvalidOperationException("Type Mermaid non supporté.")
            };

            LoadDocumentIntoShell(document);
        }
        catch (Exception ex)
        {
            GetInspector().MermaidOutputText = $"Import Mermaid failed: {ex.Message}";
        }
    }

    private static MermaidImportKind DetectMermaidImportKind(string mermaidText)
    {
        var decoded = WebUtility.HtmlDecode(mermaidText ?? string.Empty);

        var lines = decoded
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line == "```" || line.Equals("```mermaid", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("%%", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("stateDiagram-v2", StringComparison.OrdinalIgnoreCase))
                return MermaidImportKind.StateDiagram;

            if (line.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("graph", StringComparison.OrdinalIgnoreCase))
                return MermaidImportKind.Flowchart;

            break;
        }

        throw new InvalidOperationException(
            "Le texte Mermaid doit commencer par 'flowchart'/'graph' ou 'stateDiagram-v2'.");
    }

    private void LoadDocumentIntoShell(DiagramDocument document)
    {
        GetWorkspace().ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        _history.Clear();

        _documentService.LoadDocument(document);

        if (document.Kind == DiagramKind.Flowchart)
        {
            _currentDiagramFlowDirection = document.Direction switch
            {
                DocumentFlowDirection.TB => DiagramFlowDirection.TB,
                DocumentFlowDirection.RL => DiagramFlowDirection.RL,
                DocumentFlowDirection.BT => DiagramFlowDirection.BT,
                _ => DiagramFlowDirection.LR
            };
        }

        ApplyDiagramKindToUi();
        ApplyDocumentDirectionToUi();
        GetWorkspace().LoadCurrentDocument(_currentDiagramFlowDirection);
        RefreshInspector();
        Focus();
    }

    private void ApplyDocumentDirectionToUi()
    {
        if (!_uiReady)
            return;

        _suspendFlowDirectionHandling = true;
        GetFlowDirectionComboBox().SelectedIndex = _currentDiagramFlowDirection switch
        {
            DiagramFlowDirection.TB => 1,
            DiagramFlowDirection.RL => 2,
            DiagramFlowDirection.BT => 3,
            _ => 0
        };
        _suspendFlowDirectionHandling = false;
    }

    // =========================================================
    // Inspector refresh
    // =========================================================
    private void RefreshInspector()
    {
        if (!_uiReady)
            return;

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            RefreshFlowchartInspector();
            return;
        }

        RefreshStateInspector();
    }

    private void RefreshFlowchartInspector()
    {
        var canvas = GetWorkspace().GetCanvas();

        var state = _inspectorStateService.BuildState<NodeControl, EdgeControl>(
            isNodeActive: node => canvas.Children.Contains(node),
            resolveNodeId: node => (node.DataContext as Node)?.Id.Value,
            isEdgeActive: edge => canvas.Children.Contains(edge),
            resolveEdgeEndpoints: edge =>
            {
                var sourceNode = edge.SourceNode.DataContext as Node;
                var targetNode = edge.TargetNode.DataContext as Node;

                if (sourceNode == null || targetNode == null)
                    return null;

                return (sourceNode.Id.Value, targetNode.Id.Value);
            });

        if (state.ClearSelectionRequested)
        {
            _selectionService.ClearSelection();

            state = _inspectorStateService.BuildState<NodeControl, EdgeControl>(
                isNodeActive: node => canvas.Children.Contains(node),
                resolveNodeId: node => (node.DataContext as Node)?.Id.Value,
                isEdgeActive: edge => canvas.Children.Contains(edge),
                resolveEdgeEndpoints: edge =>
                {
                    var sourceNode = edge.SourceNode.DataContext as Node;
                    var targetNode = edge.TargetNode.DataContext as Node;

                    if (sourceNode == null || targetNode == null)
                        return null;

                    return (sourceNode.Id.Value, targetNode.Id.Value);
                });
        }

        GetInspector().ApplyState(
            nodeSectionEnabled: state.NodeSectionEnabled,
            nodeLabel: state.NodeLabel,
            nodeStyleIndex: state.NodeStyleIndex,
            nodeStyleEnabled: state.NodeSectionEnabled,
            edgeSectionEnabled: state.EdgeSectionEnabled,
            edgeLabel: state.EdgeLabel,
            edgeStyleIndex: state.EdgeStyleIndex,
            edgeDirectionIndex: state.EdgeDirectionIndex,
            edgeStyleEnabled: state.EdgeSectionEnabled,
            edgeDirectionEnabled: state.EdgeSectionEnabled,
            edgeStyleApplyEnabled: state.EdgeSectionEnabled);
    }

    private void RefreshStateInspector()
    {
        var selectedStateNode = GetWorkspace().GetSelectedStateNode();
        if (selectedStateNode?.DataContext is StateNode stateNode &&
            GetWorkspace().GetCanvas().Children.Contains(selectedStateNode))
        {
            var labelEnabled = stateNode.Kind == StateNodeKind.Normal;

            GetInspector().ApplyState(
                nodeSectionEnabled: labelEnabled,
                nodeLabel: stateNode.Label,
                nodeStyleIndex: 0,
                nodeStyleEnabled: false,
                edgeSectionEnabled: false,
                edgeLabel: string.Empty,
                edgeStyleIndex: 0,
                edgeDirectionIndex: 0,
                edgeStyleEnabled: false,
                edgeDirectionEnabled: false,
                edgeStyleApplyEnabled: false);
            return;
        }

        var selectedTransition = GetWorkspace().GetSelectedStateTransition();
        if (selectedTransition != null &&
            GetWorkspace().GetCanvas().Children.Contains(selectedTransition))
        {
            GetInspector().ApplyState(
                nodeSectionEnabled: false,
                nodeLabel: string.Empty,
                nodeStyleIndex: 0,
                nodeStyleEnabled: false,
                edgeSectionEnabled: true,
                edgeLabel: selectedTransition.Model.Label,
                edgeStyleIndex: 0,
                edgeDirectionIndex: 0,
                edgeStyleEnabled: false,
                edgeDirectionEnabled: false,
                edgeStyleApplyEnabled: false);
            return;
        }

        GetInspector().ApplyState(
            nodeSectionEnabled: false,
            nodeLabel: string.Empty,
            nodeStyleIndex: 0,
            nodeStyleEnabled: false,
            edgeSectionEnabled: false,
            edgeLabel: string.Empty,
            edgeStyleIndex: 0,
            edgeDirectionIndex: 0,
            edgeStyleEnabled: false,
            edgeDirectionEnabled: false,
            edgeStyleApplyEnabled: false);
    }

    // =========================================================
    // Inspector actions
    // =========================================================
    private void OnApplyNodeLabelRequested(object? sender, EventArgs e)
    {
        GetWorkspace().ApplySelectedNodeLabel(GetInspector().NodeLabelText);
    }

    private void OnApplyNodeStyleRequested(object? sender, EventArgs e)
    {
        GetWorkspace().ApplySelectedNodeStyle(GetInspector().NodeStyleIndex);
    }

    private void OnApplyEdgeLabelRequested(object? sender, EventArgs e)
    {
        GetWorkspace().ApplySelectedEdgeLabel(GetInspector().EdgeLabelText);
    }

    private void OnApplyEdgeStyleRequested(object? sender, EventArgs e)
    {
        GetWorkspace().ApplySelectedEdgeStyle(
            GetInspector().EdgeStyleIndex,
            GetInspector().EdgeDirectionIndex);
    }

    // =========================================================
    // Flow direction
    // =========================================================
    private void OnFlowDirectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suspendFlowDirectionHandling)
            return;

        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        if (sender is not ComboBox combo)
            return;

        if (combo.SelectedItem is ComboBoxItem item &&
            item.Content is string value)
        {
            _currentDiagramFlowDirection = value switch
            {
                "RL" => DiagramFlowDirection.RL,
                "TB" => DiagramFlowDirection.TB,
                "BT" => DiagramFlowDirection.BT,
                _ => DiagramFlowDirection.LR
            };

            if (!_uiReady)
                return;

            GetWorkspace().SetFlowDirection(_currentDiagramFlowDirection, syncDocument: true);
        }
    }

    // =========================================================
    // Export Mermaid
    // =========================================================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            var exportModel = BuildFlowchartExportModelFromDocument();
            GetInspector().MermaidOutputText = _flowchartExportService.Export(exportModel);
            return;
        }

        GetInspector().MermaidOutputText = _stateDiagramExportService.Export(_documentService.CurrentDocument);
    }

    private FlowchartExportModel BuildFlowchartExportModelFromDocument()
    {
        var document = _documentService.CurrentDocument;

        var model = new FlowchartExportModel
        {
            Direction = document.Direction switch
            {
                DocumentFlowDirection.TB => FlowchartExportDiagramDirection.TB,
                DocumentFlowDirection.RL => FlowchartExportDiagramDirection.RL,
                DocumentFlowDirection.BT => FlowchartExportDiagramDirection.BT,
                _ => FlowchartExportDiagramDirection.LR
            }
        };

        foreach (var node in document.Nodes)
        {
            model.Nodes.Add(new FlowchartExportNode
            {
                Id = node.Id.Value,
                Label = node.Label,
                Style = node.VisualStyle switch
                {
                    NodeVisualStyle.Rounded => FlowchartExportNodeStyle.Rounded,
                    NodeVisualStyle.Decision => FlowchartExportNodeStyle.Decision,
                    NodeVisualStyle.Circle => FlowchartExportNodeStyle.Circle,
                    _ => FlowchartExportNodeStyle.Rectangle
                }
            });
        }

        foreach (var edge in document.Edges)
        {
            model.Edges.Add(new FlowchartExportEdge
            {
                SourceId = edge.SourceNodeId.Value,
                TargetId = edge.TargetNodeId.Value,
                Label = edge.Label ?? string.Empty,
                Style = edge.Kind switch
                {
                    EdgeKind.Dashed => FlowchartExportEdgeStyle.Dashed,
                    EdgeKind.Thick => FlowchartExportEdgeStyle.Thick,
                    _ => FlowchartExportEdgeStyle.Default
                },
                Direction = edge.Direction == DocumentEdgeDirection.Reverse
                    ? FlowchartExportEdgeDirection.Reverse
                    : FlowchartExportEdgeDirection.Forward
            });
        }

        return model;
    }

    // =========================================================
    // Window key handling
    // =========================================================
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        GetWorkspace().HandleWindowKeyDown(e);
    }

    private void SyncWorkspaceDocumentIfNeeded()
    {
        if (CurrentDiagramKind == DiagramKind.Flowchart)
            GetWorkspace().SyncFlowchartDocument();
    }
}
