using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Sensors;
using Velum.Configuration;
using Velum.Isida;
using Velum.ReactiveCore;
using Velum.UI.ProductRegistry;
using Velum.Properties;
using Velum.SolidHomeostasis;
using Xarial.XCad.Base.Attributes;

namespace Velum.UI
{
  /// <summary>
  /// Содержимое вкладки «Агент» на правой панели задач SolidWorks (Task Pane).
  /// </summary>
  [Title("Агент")]
  [Icon(typeof(Resources), nameof(Resources.AgentPmp))]
  public sealed partial class VelumAgentTaskPane : UserControl
  {
    /// <summary>Размер ячейки сетки, пикс. (кнопка с Dock=Fill и Margin — квадрат визуально).</summary>
    private const float BrickCellOuterPx = 24f;

    private const int ScrollContentPadding = 2;
    private const int SectionGap = 2;

    private const int MessageInputHeightPx = 56;
    private const int SwCommandBufferHeightPx = 40;
    private const int SwFlowMinHeightPx = 28;
    /// <summary>Минимальная высота окна вывода; при свободном месте внизу панели растягивается до низа viewport.</summary>
    private const int AgentOutputMinHeightPx = 64;
    private const int SendButtonHeightPx = 26;
    private const int SendButtonTopMarginPx = 2;
    private const int OperatorInfluencesRowHeightPx = 26;
    private const int PrimariesBufferButtonHeightPx = 24;
    private const int PrimariesBufferButtonHorizontalPaddingPx = 12;
    private const int PrimariesBufferButtonVerticalPaddingPx = 6;
    private const int ParamsStylesToggleMinHeightPx = 22;

    /// <summary>Высота закреплённой шапки «Состояние» + CAD + кнопки Н/В (вне прокрутки, как в 2026_05_13).</summary>
    private const float HeaderPanelHeightPx = 56f;

    /// <summary>Голубой фон для активной метрики без давления.</summary>
    private static readonly Color MetricBrickIdleColor = Color.FromArgb(173, 216, 230);

    /// <summary>Красный фон для метрики, которая давит на параметры.</summary>
    private static readonly Color MetricBrickPressingColor = Color.FromArgb(220, 55, 55);

    /// <summary>Серый фон для отключённой метрики.</summary>
    private static readonly Color MetricBrickDisabledColor = Color.FromArgb(180, 180, 180);

    private bool _pulseHooked;

    private readonly List<Button> _mosaicMetricBricks = new List<Button>();
    private string _metricSignature = string.Empty;
    private string _cadEnvironmentUiSignature = string.Empty;
    private Button _metricBrickUnderMouse;

    /// <summary>Последнее применённое число колонок мозаики метрик.</summary>
    private int _mosaicMetricColumnsApplied = -1;

    /// <summary>Таймер пульсации индикатора обратного отсчёта (1000 мс = 1 пульс/с).</summary>
    private Timer _countdownPulseTimer;
    /// <summary>Состояние пульсации цвета (для чередования яркого и приглушённого оттенка).</summary>
    private int _countdownColorPulseState;
    /// <summary>Последнее известное состояние гомеостаза (для обнаружения перехода Bad→норм).</summary>
    private AppGlobalState.HomeostasisState _lastStateBeforePulse;

    /// <summary>«В» — воскрешение разрешено (мёртв и пульсация остановлена).</summary>
    private bool _headerReviveInvocationAllowed;

    private readonly List<int> _operatorInfluenceIds = new List<int>();

    private Button _btnHeaderHelp;

    /// <summary>Развёрнут ли блок «Состояние и метрики среды».</summary>
    private bool _metricsSectionExpanded = true;

    /// <summary>Режим «агент мёртв»: только строка состояния и «В», без остального содержимого вкладки.</summary>
    private bool _agentDeadAwaitReviveChrome;

    /// <summary>Пульсация выключена, агент жив: шапка с командами Velum и переключатель секции, без монитора.</summary>
    private bool _agentIdleChrome;

    private static readonly Color HeaderNormOnBack = Color.FromArgb(255, 224, 130);
    private static readonly Color HeaderNormOnFore = Color.Black;
    private static readonly Color HeaderNormOnBorder = Color.FromArgb(212, 160, 23);
    /// <summary>Текст «НОРМА» в шапке: насыщенный золотисто-жёлтый, читаемый на светлом фоне.</summary>
    private static readonly Color StateNormalFore = Color.FromArgb(196, 148, 0);
    private static readonly Color HeaderReviveOnBack = Color.FromArgb(76, 175, 80);
    private static readonly Color HeaderReviveOnFore = Color.White;
    private static readonly Color HeaderReviveOnBorder = Color.FromArgb(46, 125, 50);
    private static readonly Color HeaderHomeoOffBack = Color.FromArgb(200, 200, 200);
    private static readonly Color HeaderHomeoOffFore = Color.FromArgb(110, 110, 110);
    private static readonly Color HeaderHomeoOffBorder = Color.FromArgb(160, 160, 160);

    /// <summary>Запуск пульсации (команда «Старт» тулбара Velum).</summary>
    internal Action VelumPulseStartRequested;

    /// <summary>Остановка пульсации (команда «Стоп»).</summary>
    internal Action VelumPulseStopRequested;

    /// <summary>Открыть настройки проекта (команда «Настройки»).</summary>
    internal Action VelumProjectSettingsRequested;

    /// <summary>
    /// Создаёт экземпляр элемента управления для панели задач.
    /// </summary>
    public VelumAgentTaskPane()
    {
      InitializeComponent();
      DoubleBuffered = true;
      _btnHeaderHelp = VelumFormHelp.CreateToolbarHelpButton(VelumHelpTopics.AgentTaskPane, this);
      _btnHeaderHelp.FlatStyle = FlatStyle.Flat;
      _btnHeaderHelp.Margin = new Padding(0, 0, 2, 0);
      _btnHeaderHelp.Size = new Size(24, 24);
      _flowHeaderHomeoButtons.Controls.Add(_btnHeaderHelp);
      Load += VelumAgentTaskPane_Load;
    }

    /// <summary>
    /// Отписка от <see cref="ISIDA.Common.GlobalTimer"/> при уничтожении дескриптора.
    /// </summary>
    /// <param name="e">Аргументы события.</param>
    protected override void OnHandleDestroyed(EventArgs e)
    {
      if (_btnSend != null)
        _btnSend.Click -= OnSendMessageClick;
      if (_chkVerbalAuthoritative != null)
        _chkVerbalAuthoritative.CheckedChanged -= OnVerbalAuthoritativeCheckedChanged;
      if (_chkAutoAddSensors != null)
        _chkAutoAddSensors.CheckedChanged -= OnAutoAddSensorsCheckedChanged;
      if (_btnViewCommandPrimariesBuffer != null)
        _btnViewCommandPrimariesBuffer.Click -= OnViewCommandPrimariesBufferClick;
      if (_btnViewVerbalPrimariesBuffer != null)
        _btnViewVerbalPrimariesBuffer.Click -= OnViewVerbalPrimariesBufferClick;
      if (_btnOperatorInfluencesPick != null)
        _btnOperatorInfluencesPick.Click -= OnOperatorInfluencesPickClick;
      if (_btnSwBufferClear != null)
        _btnSwBufferClear.Click -= OnSwBufferClearClick;
      if (_btnMetricsSettingsPick != null)
        _btnMetricsSettingsPick.Click -= OnMetricsSettingsPickClick;
      if (_btnHeaderNormHomeostasis != null)
        _btnHeaderNormHomeostasis.Click -= OnHeaderNormHomeostasisClick;
      if (_btnHeaderReviveAgent != null)
        _btnHeaderReviveAgent.Click -= OnHeaderReviveAgentClick;
      if (_btnHeaderVelumPulseStart != null)
        _btnHeaderVelumPulseStart.Click -= OnHeaderVelumPulseStartClick;
      if (_btnHeaderVelumPulseStop != null)
        _btnHeaderVelumPulseStop.Click -= OnHeaderVelumPulseStopClick;
      if (_btnHeaderVelumProjectSettings != null)
        _btnHeaderVelumProjectSettings.Click -= OnHeaderVelumProjectSettingsClick;
      if (_btnToggleParamsAndStyles != null)
        _btnToggleParamsAndStyles.Click -= OnToggleParamsAndStylesClick;
      VelumSolidEnvironmentBridge.SolidCommandBufferChanged -= OnSolidCommandBufferChanged;
      VelumProductRegistryIntegrityScheduler.ScanStateChanged -= OnRegistryScanStateChanged;
      UnhookPulseEvents();
      if (_countdownPulseTimer != null)
      {
        _countdownPulseTimer.Stop();
        _countdownPulseTimer.Dispose();
        _countdownPulseTimer = null;
      }
      base.OnHandleDestroyed(e);
    }

    private void VelumAgentTaskPane_Load(object sender, EventArgs e)
    {
      HookPulseEvents();
      _scrollPanel.Resize += ScrollPanel_Resize;
      _btnSend.Click += OnSendMessageClick;
      if (_chkVerbalAuthoritative != null)
      {
        _chkVerbalAuthoritative.Checked = false;
        ApplyAuthoritativeModesFromPanel();
        _chkVerbalAuthoritative.CheckedChanged += OnVerbalAuthoritativeCheckedChanged;
      }
      if (_chkAutoAddSensors != null)
      {
        _chkAutoAddSensors.Checked = false;
        RefreshPrimariesHintLabels();
        _chkAutoAddSensors.CheckedChanged += OnAutoAddSensorsCheckedChanged;
      }
      if (_btnViewCommandPrimariesBuffer != null)
        _btnViewCommandPrimariesBuffer.Click += OnViewCommandPrimariesBufferClick;
      if (_btnViewVerbalPrimariesBuffer != null)
        _btnViewVerbalPrimariesBuffer.Click += OnViewVerbalPrimariesBufferClick;
      if (_btnOperatorInfluencesPick != null)
        _btnOperatorInfluencesPick.Click += OnOperatorInfluencesPickClick;
      _btnSwBufferClear.Click += OnSwBufferClearClick;
      if (_btnHeaderNormHomeostasis != null)
        _btnHeaderNormHomeostasis.Click += OnHeaderNormHomeostasisClick;
      if (_btnHeaderReviveAgent != null)
        _btnHeaderReviveAgent.Click += OnHeaderReviveAgentClick;
      ConfigureHeaderVelumCommandButtons();
      if (_btnHeaderVelumPulseStart != null)
        _btnHeaderVelumPulseStart.Click += OnHeaderVelumPulseStartClick;
      if (_btnHeaderVelumPulseStop != null)
        _btnHeaderVelumPulseStop.Click += OnHeaderVelumPulseStopClick;
      if (_btnHeaderVelumProjectSettings != null)
        _btnHeaderVelumProjectSettings.Click += OnHeaderVelumProjectSettingsClick;
      if (_btnToggleParamsAndStyles != null)
      {
        _btnToggleParamsAndStyles.Click += OnToggleParamsAndStylesClick;
        RefreshToggleParamsAndStylesButtonText();
      }

      if (_btnMetricsSettingsPick != null)
        _btnMetricsSettingsPick.Click += OnMetricsSettingsPickClick;

      VelumSolidEnvironmentBridge.SolidCommandBufferChanged += OnSolidCommandBufferChanged;
      VelumProductRegistryIntegrityScheduler.ScanStateChanged += OnRegistryScanStateChanged;
      RefreshHeaderHomeostasisTooltips();
      if (_parameterToolTip != null && _btnOperatorInfluencesPick != null &&
          _txtOperatorInfluencesDisplay != null)
      {
        _parameterToolTip.SetToolTip(_btnOperatorInfluencesPick, "Выбор воздействий на параметры агента");
        _parameterToolTip.SetToolTip(_txtOperatorInfluencesDisplay, "Прямое воздействие оператора");
      }
      RefreshOperatorInfluencesDisplay();
      RefreshSwBufferText();
      RequestRefresh();

      // Инициализация таймера пульсации индикатора обратного отсчёта (1 пульс = 1 секунда).
      _countdownPulseTimer = new Timer { Interval = 1000 };
      _countdownPulseTimer.Tick += OnCountdownPulseTick;
    }

    private void ScrollPanel_Resize(object sender, EventArgs e)
    {
      LayoutScrollContents();
    }

    /// <summary>
    /// Внутренняя ширина контента прокрутки (как у подписей и полей), для подбора числа колонок мозаики.
    /// </summary>
    private int GetMosaicViewportInnerWidth()
    {
      if (_scrollPanel == null)
        return 360;
      int pad = ScrollContentPadding;
      return Math.Max(1, _scrollPanel.ClientSize.Width - 2 * pad);
    }

    /// <summary>
    /// Сколько кирпичей помещается в ряд по ширине вьюпорта (не больше числа кирпичей).
    /// </summary>
    private static int ComputeMosaicColumnsForCount(int viewportInnerW, int brickCount, int gridPaddingHorizontal)
    {
      if (brickCount <= 0)
        return 1;
      double usable = viewportInnerW - gridPaddingHorizontal;
      if (usable < 1d)
        usable = 1d;
      int fromWidth = (int)Math.Floor(usable / (double)BrickCellOuterPx);
      if (fromWidth < 1)
        fromWidth = 1;
      return Math.Min(brickCount, fromWidth);
    }

    /// <summary>
    /// Меняет только раскладку <see cref="TableLayoutPanel"/> (строка/колонка), кирпичи не пересоздаются.
    /// </summary>
    private static void RelayoutMosaicGrid(TableLayoutPanel grid, List<Button> bricks, int columns)
    {
      if (grid == null || bricks == null || bricks.Count == 0)
        return;

      int n = bricks.Count;
      int cols = Math.Max(1, Math.Min(columns, n));
      int rows = (n + cols - 1) / cols;

      grid.SuspendLayout();
      try
      {
        grid.ColumnStyles.Clear();
        grid.RowStyles.Clear();
        grid.ColumnCount = cols;
        grid.RowCount = rows;

        for (int c = 0; c < cols; c++)
          grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BrickCellOuterPx));
        for (int r = 0; r < rows; r++)
          grid.RowStyles.Add(new RowStyle(SizeType.Absolute, BrickCellOuterPx));

        for (int i = 0; i < n; i++)
        {
          Button b = bricks[i];
          grid.SetColumn(b, i % cols);
          grid.SetRow(b, i / cols);
        }
      }
      finally
      {
        grid.ResumeLayout(true);
      }
    }

    /// <summary>
    /// Подгоняет число колонок мозаик к ширине панели (без смены набора кирпичей).
    /// </summary>
    private void SyncMosaicTablesToViewportWidth(int viewportInnerW)
    {
      if (_brickGrid == null)
        return;

      int n = _mosaicMetricBricks.Count;
      int pad = _brickGrid.Padding.Horizontal;
      int want = ComputeMosaicColumnsForCount(viewportInnerW, n, pad);

      if (n > 0 && (want != _mosaicMetricColumnsApplied || want != _brickGrid.ColumnCount))
      {
        RelayoutMosaicGrid(_brickGrid, _mosaicMetricBricks, want);
        _mosaicMetricColumnsApplied = want;
      }
      else if (n == 0)
        _mosaicMetricColumnsApplied = -1;

      ApplyMetricBrickGridSizeFromMosaic();
    }

    private void SetLowerAgentScrollSectionsVisible(bool visible)
    {
      if (_flowSwBufferCaption != null)
        _flowSwBufferCaption.Visible = visible;
      if (_pnlSwBufferButtonsRow != null)
        _pnlSwBufferButtonsRow.Visible = visible;
      if (_btnViewCommandPrimariesBuffer != null)
        _btnViewCommandPrimariesBuffer.Visible = visible;
      if (_btnSwBufferClear != null)
        _btnSwBufferClear.Visible = visible;
      if (_txtSwCommandBuffer != null)
        _txtSwCommandBuffer.Visible = visible;
      if (_tblSwBufferRow != null)
        _tblSwBufferRow.Visible = visible;
      if (_lblOperatorInfluencesCaption != null)
        _lblOperatorInfluencesCaption.Visible = visible;
      if (_pnlOperatorInfluences != null)
        _pnlOperatorInfluences.Visible = visible;
      if (_flowInputCaption != null)
        _flowInputCaption.Visible = visible;
      if (_btnViewVerbalPrimariesBuffer != null)
        _btnViewVerbalPrimariesBuffer.Visible = visible;
      if (_txtMessageInput != null)
        _txtMessageInput.Visible = visible;
      if (_pnlSendRow != null)
        _pnlSendRow.Visible = visible;
      if (_lblOutputCaption != null)
        _lblOutputCaption.Visible = visible;
      if (_txtAgentOutput != null)
        _txtAgentOutput.Visible = visible;
    }

    /// <summary>
    /// Строка «Актуальная проблема» под мозаикой метрик.
    /// </summary>
    private void LayoutActualProblemRow(int x, ref int y, int innerW, int gap)
    {
      if (_pnlActualProblemRow == null || _lblActualProblemCaption == null || _lblActualProblemValue == null)
        return;

      _lblActualProblemCaption.Location = new Point(0, 0);
      int captionW = _lblActualProblemCaption.PreferredSize.Width;
      int valueX = captionW + 6;
      _lblActualProblemValue.MaximumSize = new Size(Math.Max(1, innerW - valueX), 0);
      _lblActualProblemValue.Location = new Point(valueX, 0);

      int rowH = Math.Max(
          _lblActualProblemCaption.PreferredSize.Height,
          _lblActualProblemValue.PreferredSize.Height);
      _pnlActualProblemRow.Location = new Point(x, y);
      _pnlActualProblemRow.Width = innerW;
      _pnlActualProblemRow.Height = Math.Max(1, rowH);
      y += _pnlActualProblemRow.Height + gap;
    }

    /// <summary>
    /// Обновляет подпись актуальной проблемы из гомеостаза (DominantParam при hasProblem).
    /// </summary>
    private void RefreshActualProblemField()
    {
      if (_lblActualProblemValue == null || _lblActualProblemValue.IsDisposed)
        return;

      string text = string.Empty;
      if (VelumProblemContextHint.TryGetActualProblem(out bool hasProblem, out string problemName)
          && hasProblem
          && !string.IsNullOrWhiteSpace(problemName))
      {
        text = problemName;
      }

      if (_lblActualProblemValue.Text == text)
        return;
      _lblActualProblemValue.Text = text;
    }

    private void ClearActualProblemField()
    {
      if (_lblActualProblemValue == null || _lblActualProblemValue.IsDisposed)
        return;
      if (_lblActualProblemValue.Text.Length == 0)
        return;
      _lblActualProblemValue.Text = string.Empty;
    }

    /// <summary>
    /// Стабильный размер кнопки «Буфер» по тексту (без <see cref="Control.PreferredSize"/> — иначе при AutoSize и SetBounds ширина растёт на каждом пульсе).
    /// </summary>
    private static Size MeasurePrimariesBufferButtonSize(Button button)
    {
      Size text = TextRenderer.MeasureText(
          button.Text,
          button.Font,
          Size.Empty,
          TextFormatFlags.SingleLine);
      int width = text.Width + PrimariesBufferButtonHorizontalPaddingPx;
      int height = Math.Max(
          PrimariesBufferButtonHeightPx,
          text.Height + PrimariesBufferButtonVerticalPaddingPx);
      return new Size(width, height);
    }

    /// <summary>
    /// Размеры сеток и положение блоков в области прокрутки.
    /// </summary>
    private void LayoutScrollContents()
    {
      if (_scrollPanel == null || !_scrollPanel.Visible || _scrollHost == null || !_scrollHost.Visible ||
          _btnToggleParamsAndStyles == null ||
          _pnlParametersCaptionRow == null || _lblParametersCaption == null ||
          _btnMetricsSettingsPick == null || _brickGrid == null ||
          _pnlActualProblemRow == null || _lblActualProblemCaption == null || _lblActualProblemValue == null ||
          _lblSwBufferCaption == null || _flowSwBufferCaption == null ||
          _pnlSwBufferButtonsRow == null ||
          _btnViewCommandPrimariesBuffer == null || _btnSwBufferClear == null ||
          _txtSwCommandBuffer == null || _tblSwBufferRow == null ||
          _lblOperatorInfluencesCaption == null || _pnlOperatorInfluences == null ||
          _txtOperatorInfluencesDisplay == null || _btnOperatorInfluencesPick == null ||
          _lblInputCaption == null || _flowInputCaption == null ||
          _btnViewVerbalPrimariesBuffer == null || _txtMessageInput == null || _pnlSendRow == null ||
          _btnSend == null || _lblOutputCaption == null || _txtAgentOutput == null)
        return;

      int pad = ScrollContentPadding;
      int gap = SectionGap;
      int cw = _scrollPanel.ClientSize.Width;
      int innerW = Math.Max(1, cw - 2 * pad);
      int x = pad;
      int y = pad;
      bool headerVisible = false;
      float headerRowHeight = 0f;

      if (_agentDeadAwaitReviveChrome)
      {
        _btnToggleParamsAndStyles.Visible = false;
        _pnlParametersCaptionRow.Visible = false;
        _brickGrid.Visible = false;
        _pnlActualProblemRow.Visible = false;
        SetLowerAgentScrollSectionsVisible(false);
        headerVisible = true;
        headerRowHeight = HeaderPanelHeightPx;
        _scrollHost.Size = new Size(cw, Math.Max(pad, 1));
        ApplyHeaderChromeForCurrentMode();
        if (_rootLayout != null && _rootLayout.RowCount >= 2)
          _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, headerRowHeight);
        return;
      }

      if (_agentIdleChrome)
      {
        _btnToggleParamsAndStyles.Visible = true;
        SetLowerAgentScrollSectionsVisible(false);
        _pnlParametersCaptionRow.Visible = false;
        _brickGrid.Visible = false;
        _pnlActualProblemRow.Visible = false;

        int toggleHIdle = Math.Max(
            ParamsStylesToggleMinHeightPx,
            _btnToggleParamsAndStyles.PreferredSize.Height);
        _btnToggleParamsAndStyles.SetBounds(x, y, innerW, toggleHIdle);
        y += toggleHIdle + pad;

        headerVisible = _metricsSectionExpanded;
        headerRowHeight = headerVisible ? HeaderPanelHeightPx : 0f;
        ApplyHeaderChromeForCurrentMode();
        if (_rootLayout != null && _rootLayout.RowCount >= 2)
          _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, headerRowHeight);
        _scrollHost.Size = new Size(cw, Math.Max(y, 1));
        return;
      }

      _btnToggleParamsAndStyles.Visible = true;
      SetLowerAgentScrollSectionsVisible(true);

      SyncMosaicTablesToViewportWidth(innerW);

      int toggleH = Math.Max(
          ParamsStylesToggleMinHeightPx,
          _btnToggleParamsAndStyles.PreferredSize.Height);
      _btnToggleParamsAndStyles.SetBounds(x, y, innerW, toggleH);
      y += toggleH + gap;

      bool expanded = _metricsSectionExpanded;
      headerVisible = expanded;
      headerRowHeight = headerVisible ? HeaderPanelHeightPx : 0f;
      ApplyHeaderChromeForCurrentMode();
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
        _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, headerRowHeight);

      _pnlParametersCaptionRow.Visible = expanded;
      _brickGrid.Visible = expanded;
      _pnlActualProblemRow.Visible = expanded;

      if (expanded)
      {
        int metricsPickW = (int)Math.Round(BrickCellOuterPx);
        _pnlParametersCaptionRow.Location = new Point(x, y);
        _pnlParametersCaptionRow.Width = innerW;
        _pnlParametersCaptionRow.Height = Math.Max(
            _lblParametersCaption.Height,
            metricsPickW);
        _lblParametersCaption.MaximumSize = new Size(Math.Max(1, innerW - metricsPickW - 4), 0);
        _lblParametersCaption.Location = new Point(0, 0);
        _btnMetricsSettingsPick.SetBounds(
            Math.Max(0, innerW - metricsPickW),
            0,
            Math.Min(metricsPickW, innerW),
            Math.Min(metricsPickW, _pnlParametersCaptionRow.Height));
        y += _pnlParametersCaptionRow.Height + gap;

        _brickGrid.Location = new Point(x, y);
        y += _brickGrid.Height + gap;

        LayoutActualProblemRow(x, ref y, innerW, gap);
      }

      _flowSwBufferCaption.MaximumSize = new Size(innerW, 0);
      _flowSwBufferCaption.Location = new Point(x, y);
      _flowSwBufferCaption.Width = innerW;
      y += _flowSwBufferCaption.Height + gap;

      Size cmdBufBtnSize = MeasurePrimariesBufferButtonSize(_btnViewCommandPrimariesBuffer);
      int clearBtnW = Math.Max(71, TextRenderer.MeasureText(
          _btnSwBufferClear.Text,
          _btnSwBufferClear.Font,
          Size.Empty,
          TextFormatFlags.SingleLine).Width + 16);
      int swButtonsRowH = Math.Max(cmdBufBtnSize.Height, _btnSwBufferClear.Height);
      _pnlSwBufferButtonsRow.Location = new Point(x, y);
      _pnlSwBufferButtonsRow.Width = innerW;
      _pnlSwBufferButtonsRow.Height = swButtonsRowH;
      _btnViewCommandPrimariesBuffer.SetBounds(
          0,
          0,
          Math.Min(innerW, cmdBufBtnSize.Width),
          swButtonsRowH);
      _btnSwBufferClear.SetBounds(
          Math.Max(0, innerW - clearBtnW),
          0,
          Math.Min(clearBtnW, innerW),
          swButtonsRowH);
      y += swButtonsRowH + gap;

      _txtSwCommandBuffer.Location = new Point(x, y);
      _txtSwCommandBuffer.Width = innerW;
      _txtSwCommandBuffer.Height = SwCommandBufferHeightPx;
      y += _txtSwCommandBuffer.Height + gap;

      _tblSwBufferRow.Location = new Point(x, y);
      _tblSwBufferRow.Width = innerW;
      _tblSwBufferRow.PerformLayout();
      int swRowH = Math.Max(SwFlowMinHeightPx, _tblSwBufferRow.PreferredSize.Height);
      _tblSwBufferRow.Height = swRowH;
      y += _tblSwBufferRow.Height + gap;

      _lblOperatorInfluencesCaption.MaximumSize = new Size(innerW, 0);
      _lblOperatorInfluencesCaption.Location = new Point(x, y);
      y += _lblOperatorInfluencesCaption.Height + gap;

      int pickW = (int)Math.Round(BrickCellOuterPx);
      _pnlOperatorInfluences.Location = new Point(x, y);
      _pnlOperatorInfluences.Width = innerW;
      _pnlOperatorInfluences.Height = OperatorInfluencesRowHeightPx;
      _btnOperatorInfluencesPick.SetBounds(
          Math.Max(0, innerW - pickW),
          0,
          Math.Min(pickW, innerW),
          Math.Min(pickW, OperatorInfluencesRowHeightPx));
      int textW = Math.Max(1, innerW - _btnOperatorInfluencesPick.Width - 4);
      int textH = Math.Max(1, OperatorInfluencesRowHeightPx - 4);
      _txtOperatorInfluencesDisplay.SetBounds(0, 2, textW, textH);
      y += _pnlOperatorInfluences.Height + gap;

      _flowInputCaption.MaximumSize = new Size(innerW, 0);
      _flowInputCaption.Location = new Point(x, y);
      _flowInputCaption.Width = innerW;
      y += _flowInputCaption.Height + gap;

      Size verbalBufBtnSize = MeasurePrimariesBufferButtonSize(_btnViewVerbalPrimariesBuffer);
      _btnViewVerbalPrimariesBuffer.SetBounds(x, y, Math.Min(innerW, verbalBufBtnSize.Width), verbalBufBtnSize.Height);
      y += verbalBufBtnSize.Height + gap;

      _txtMessageInput.Location = new Point(x, y);
      _txtMessageInput.Width = innerW;
      _txtMessageInput.Height = MessageInputHeightPx;
      y += _txtMessageInput.Height + SendButtonTopMarginPx;

      _pnlSendRow.Location = new Point(x, y);
      _pnlSendRow.Width = innerW;
      LayoutSendRowChildren(innerW);
      y += _pnlSendRow.Height + gap;

      _lblOutputCaption.MaximumSize = new Size(innerW, 0);
      _lblOutputCaption.Location = new Point(x, y);
      y += _lblOutputCaption.Height + gap;

      _txtAgentOutput.Location = new Point(x, y);
      _txtAgentOutput.Width = innerW;
      int viewportH = _scrollPanel.ClientSize.Height;
      int slackBelowOutput = viewportH > 0 ? viewportH - y - pad : 0;
      int outputH = Math.Max(AgentOutputMinHeightPx, slackBelowOutput);
      _txtAgentOutput.Height = outputH;
      y += _txtAgentOutput.Height + pad;

      _scrollHost.Size = new Size(cw, Math.Max(y, 1));
    }

    private void RefreshToggleParamsAndStylesButtonText()
    {
      if (_btnToggleParamsAndStyles == null || _btnToggleParamsAndStyles.IsDisposed)
        return;
      _btnToggleParamsAndStyles.Text = _metricsSectionExpanded
          ? "▼ Состояние и метрики среды агента"
          : "▶ Состояние и метрики среды агента";
    }

    private void OnToggleParamsAndStylesClick(object sender, EventArgs e)
    {
      _metricsSectionExpanded = !_metricsSectionExpanded;
      RefreshToggleParamsAndStylesButtonText();
      ApplyHeaderChromeForCurrentMode();
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
      {
        _rootLayout.RowStyles[0] = new RowStyle(
            SizeType.Absolute,
            _metricsSectionExpanded ? HeaderPanelHeightPx : 0f);
      }

      LayoutScrollContents();
    }

    private void LayoutSendRowChildren(int innerW)
    {
      if (_pnlSendRow == null || _btnSend == null)
        return;

      int pad = 2;
      int btnH = SendButtonHeightPx - 2;
      int btnW = Math.Max(1, innerW - 2 * pad);
      _btnSend.SetBounds(pad, pad, btnW, btnH);
      _pnlSendRow.Height = pad + btnH + pad;
    }

    private void RefreshOperatorInfluencesDisplay()
    {
      if (_txtOperatorInfluencesDisplay == null || _txtOperatorInfluencesDisplay.IsDisposed)
        return;
      if (_operatorInfluenceIds.Count == 0)
      {
        _txtOperatorInfluencesDisplay.Text = string.Empty;
        return;
      }

      try
      {
        if (VelumIsidaHost.TryInitialize(out _) && InfluenceActionSystem.IsInitialized)
        {
          var byId = InfluenceActionSystem.Instance.GetAllInfluenceActions().ToDictionary(a => a.Id);
          var parts = new List<string>();
          foreach (int id in _operatorInfluenceIds.OrderBy(v => v))
          {
            if (byId.TryGetValue(id, out InfluenceActionSystem.GomeostasisInfluenceAction act) &&
                !string.IsNullOrWhiteSpace(act.Name))
              parts.Add(act.Name);
            else
              parts.Add("ID " + id);
          }

          _txtOperatorInfluencesDisplay.Text = string.Join(", ", parts);
          return;
        }
      }
      catch
      {
      }

      _txtOperatorInfluencesDisplay.Text = string.Join(
          ", ",
          _operatorInfluenceIds.OrderBy(v => v).Select(id => "ID " + id));
    }

    private void OnVerbalAuthoritativeCheckedChanged(object sender, EventArgs e)
    {
      ApplyAuthoritativeModesFromPanel();
      RefreshPrimariesHintLabels();
      LayoutScrollContents();
    }

    private void OnAutoAddSensorsCheckedChanged(object sender, EventArgs e)
    {
      RefreshPrimariesHintLabels();
      QueueCommandPrimariesToBufferIfAutoAdd();
      LayoutScrollContents();
    }

    /// <summary>
    /// Синхронизирует флажок пульта с авторитарным режимом вербального и командного каналов ISIDA.
    /// </summary>
    private void ApplyAuthoritativeModesFromPanel()
    {
      bool v = _chkVerbalAuthoritative != null && _chkVerbalAuthoritative.Checked;
      try
      {
        if (SensorySystem.IsInitialized)
        {
          SensorySystem.Instance.VerbalAuthoritativeMode = v;
          SensorySystem.Instance.CommandAuthoritativeMode = v;
        }
      }
      catch
      {
      }
    }

    private void RefreshPrimariesHintLabels()
    {
      bool authoritative = _chkVerbalAuthoritative != null && _chkVerbalAuthoritative.Checked;
      bool autoAdd = _chkAutoAddSensors != null && _chkAutoAddSensors.Checked;
      if (_lblSwBufferAuthoritativeHint != null)
        _lblSwBufferAuthoritativeHint.Visible = autoAdd;
      if (_lblInputAuthoritativeHint != null)
        _lblInputAuthoritativeHint.Visible = authoritative;
    }

    private bool IsAutoAddSensorsEnabled()
    {
      return _chkAutoAddSensors != null && _chkAutoAddSensors.Checked;
    }

    private void QueueCommandPrimariesToBufferIfAutoAdd()
    {
      if (!IsAutoAddSensorsEnabled())
        return;

      try
      {
        QueueCommandPrimariesToBufferIfAutoAddFromLine(VelumSolidCommandBuffer.GetSnapshot());
      }
      catch
      {
      }
    }

    private static void QueueCommandPrimariesToBufferIfAutoAddFromLine(string commandLine)
    {
      if (string.IsNullOrWhiteSpace(commandLine))
        return;

      VelumSensorPrimariesBuffer.QueueCommandTokensIfMissing(
          VelumOperatorStimulusCodec.EnumerateCommandTokens(commandLine));
    }

    private void QueueVerbalPrimariesToBufferIfAutoAdd(string text)
    {
      if (!IsAutoAddSensorsEnabled() || string.IsNullOrWhiteSpace(text))
        return;

      try
      {
        string normalized = VelumOperatorStimulusCodec.NormalizeForVerbalChannel(text);
        VelumSensorPrimariesBuffer.QueueVerbalSymbolsIfMissing(normalized);
      }
      catch
      {
      }
    }

    private void OnViewCommandPrimariesBufferClick(object sender, EventArgs e)
    {
      ShowPrimariesBufferForm(VelumSensorPrimariesChannel.Command);
    }

    private void OnViewVerbalPrimariesBufferClick(object sender, EventArgs e)
    {
      ShowPrimariesBufferForm(VelumSensorPrimariesChannel.Verbal);
    }

    private void ShowPrimariesBufferForm(VelumSensorPrimariesChannel channel)
    {
      Form owner = FindForm();
      if (!VelumAdminAccess.TryRequireAdmin(owner, VelumAdminAccess.FormDeniedMessage))
        return;

      using (var dlg = new VelumSensorPrimariesBufferForm(channel))
      {
        if (owner != null)
          dlg.ShowDialog(owner);
        else
          dlg.ShowDialog();
      }
    }

    private void OnOperatorInfluencesPickClick(object sender, EventArgs e)
    {
      if (!VelumAdminAccess.TryRequireAdmin(FindForm(), VelumAdminAccess.FormDeniedMessage))
        return;

      if (!VelumIsidaHost.TryInitialize(out string err))
      {
        MessageBox.Show(err, "Velum", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      if (!InfluenceActionSystem.IsInitialized)
      {
        MessageBox.Show(
            "Система воздействий ISIDA не инициализирована.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> list =
          InfluenceActionSystem.Instance.GetAllInfluenceActions()
              .Where(a => a != null && !a.IsEnvironmentProbeAction)
              .ToList();

      using (var dlg = new VelumOperatorInfluencesPickerForm(
          list,
          _operatorInfluenceIds,
          VelumProblemContextHint.TryGetSuggestedOperatorInfluenceActionId()))
      {
        Form owner = FindForm();
        if (owner != null)
        {
          if (dlg.ShowDialog(owner) != DialogResult.OK)
            return;
        }
        else if (dlg.ShowDialog() != DialogResult.OK)
        {
          return;
        }

        _operatorInfluenceIds.Clear();
        if (dlg.SelectedActionIds != null)
          _operatorInfluenceIds.AddRange(dlg.SelectedActionIds);
        RefreshOperatorInfluencesDisplay();
      }
    }

    /// <summary>
    /// Внешний размер сетки метрик по числу кирпичей.
    /// </summary>
    private void ApplyMetricBrickGridSizeFromMosaic()
    {
      if (_brickGrid == null)
        return;

      int n = _mosaicMetricBricks.Count;
      int padH = _brickGrid.Padding.Horizontal;
      int padV = _brickGrid.Padding.Vertical;

      if (n == 0)
      {
        _brickGrid.Size = new Size(padH + 1, padV + 1);
        return;
      }

      int cols = _brickGrid.ColumnCount > 0 ? _brickGrid.ColumnCount : 1;
      int rows = (n + cols - 1) / cols;
      int w = padH + (int)Math.Round(cols * BrickCellOuterPx);
      int h = padV + (int)Math.Round(rows * BrickCellOuterPx);
      _brickGrid.Size = new Size(w, h);
    }

    /// <summary>
    /// После <c>VelumIsidaHost.Shutdown</c> вызывается <see cref="ISIDA.Common.GlobalTimer.ClearSystems"/>, который
    /// обнуляет все делегаты событий таймера — подписки панели пропадают. Вызывать из add-in после повторной
    /// инициализации ISIDA (сохранение настроек и т.д.).
    /// </summary>
    public void RehookGlobalTimerAfterIsidaReload()
    {
      UnhookPulseEvents();
      HookPulseEvents();
      RequestRefresh();
    }

    private void HookPulseEvents()
    {
      if (_pulseHooked)
        return;
      GlobalTimer.OnPulseAfterGomeostasisBeforePsychic += OnAfterGomeostasis;
      GlobalTimer.PulsationStateChanged += OnPulsationStateChanged;
      _pulseHooked = true;
    }

    private void UnhookPulseEvents()
    {
      if (!_pulseHooked)
        return;
      GlobalTimer.OnPulseAfterGomeostasisBeforePsychic -= OnAfterGomeostasis;
      GlobalTimer.PulsationStateChanged -= OnPulsationStateChanged;
      _pulseHooked = false;
    }

    private void OnPulsationStateChanged()
    {
      RequestRefresh();
    }

    private void OnRegistryScanStateChanged()
    {
      if (IsHandleCreated)
        BeginInvoke(new Action(UpdateRegistryScanStatus));
    }


    private void OnAfterGomeostasis(int pulseNumber)
    {
      RequestRefresh();
    }

    private void RequestRefresh()
    {
      if (!IsHandleCreated)
        return;
      BeginInvoke(new Action(RefreshFromEngineSafe));
    }

    private void RefreshFromEngineSafe()
    {
      if (IsDisposed)
        return;
      try
      {
        RefreshFromEngine();
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumAgentTaskPane.RefreshFromEngine: " + ex);
        try
        {
          ShowAgentMonitorUiFull();
          ClearMosaic();
          LayoutScrollContents();
        }
        catch
        {
        }
      }
      finally
      {
        ApplyAuthoritativeModesFromPanel();
      }
    }

    /// <summary>
    /// Читает состояние и метрики среды из ISIDA; мозаику пересоздаёт при смене набора.
    /// При остановке пульсации остаются переключатель секции и команды Velum; при «мертв» — шапка с «В».
    /// </summary>
    private void UpdateRegistryScanStatus()
    {
      if (_lblRegistryScanStatus == null)
        return;

      if (!VelumProductRegistryIntegrityScheduler.IsEnabled
          || !VelumProductRegistryIntegrityScheduler.IsPulsationRunning)
      {
        _lblRegistryScanStatus.Text = string.Empty;
        _lblRegistryScanStatus.Visible = false;
        return;
      }

      if (VelumProductRegistryIntegrityScheduler.IsOpenDocumentsScopeActive)
      {
        _lblRegistryScanStatus.Text = string.Empty;
        _lblRegistryScanStatus.Visible = false;
        return;
      }

      int iteration = VelumProductRegistryIntegrityScheduler.ScanIterationCount;
      int period = VelumProductRegistryIntegrityScheduler.HeavyMetricsPulsePeriod;
      string baseText = $"Сканирование №{iteration} ({period} пул.): ";

      // «выполняется…» — только когда тик реально исполняется в фоновом потоке.
      // Между тиками проходы продолжаются по курсорам, но найденные проблемы уже
      // опубликованы в кэш (pending учитывается), поэтому показываем результат.
      if (VelumProductRegistryIntegrityScheduler.IsTickInFlight)
      {
        _lblRegistryScanStatus.Text = baseText + "выполняется…";
        _lblRegistryScanStatus.ForeColor = Color.FromArgb(0, 112, 192);
        _lblRegistryScanStatus.Visible = true;
        return;
      }

      bool hasProblems = VelumProductRegistryProblemCache.HasAny;
      if (hasProblems)
      {
        _lblRegistryScanStatus.Text = baseText + "есть проблемы";
        _lblRegistryScanStatus.ForeColor = Color.FromArgb(220, 55, 55);
      }
      else
      {
        _lblRegistryScanStatus.Text = baseText + "проблем нет";
        _lblRegistryScanStatus.ForeColor = Color.FromArgb(46, 125, 50);
      }
      _lblRegistryScanStatus.Visible = true;
    }

    private void RefreshFromEngine()
    {
      if (!VelumIsidaHost.IsReady)
      {
        ApplyAgentMonitorIdleChrome();
        LayoutScrollContents();
        return;
      }

      bool pulse = GlobalTimer.IsPulsationRunning;
      bool dead = AppGlobalState.IsDead;
      if (!pulse && !dead)
      {
        ApplyAgentMonitorIdleChrome();
        LayoutScrollContents();
        return;
      }

      try
      {
         if (!pulse && dead)
         {
           ShowAgentDeadAwaitReviveChrome();
           AppGlobalState.HomeostasisState stDead = AppGlobalState.CurrentOverallState;
           _lastStateBeforePulse = stDead;
           ApplyOverallStateLabelIfChanged(stDead);
           ApplyCadEnvironmentStatusLabelIfChanged();
           UpdateHeaderHomeostasisButtons();
           RefreshVelumCommandButtons();
           LayoutScrollContents();
           return;
         }

          ShowAgentMonitorUiFull();

          AppGlobalState.HomeostasisState st = AppGlobalState.CurrentOverallState;
          ApplyOverallStateLabelIfChanged(st);

          // Обновляем индикатор ожидания/отсутствия ожидания.
          UpdateCountdownIndicatorState();

          // Если состояние перешло из Bad в нормальное — закрыть индикатор ожидания.
         // Это оценка автоматизма ответом среды: метрика перестала давить → проблема снята.
         if (_lastStateBeforePulse == AppGlobalState.HomeostasisState.Bad &&
             st != AppGlobalState.HomeostasisState.Bad)
         {
           RecipeDispatcher.HideWaitingIndicator();
         }
         _lastStateBeforePulse = st;

         ApplyCadEnvironmentStatusLabelIfChanged();

        try
        {
          List<InfluenceActionSystem.GomeostasisInfluenceAction> metrics =
              VelumSolidEnvironmentInfluenceCatalog.GetAllEnvironmentActions().ToList();
          string sig = BuildMetricSignature(metrics);
          if (sig != _metricSignature)
          {
            _metricSignature = sig;
            RebuildMetricMosaic(metrics);
          }
          else
          {
            UpdateMetricBricksOnly(metrics);
          }
        }
        catch (Exception ex)
        {
          if (VelumAppConfig.SolidHomeostasisDebugLog)
            System.Diagnostics.Trace.WriteLine("VelumAgentTaskPane metrics mosaic: " + ex);
          ClearMosaic();
        }

        RefreshActualProblemField();
        UpdateRegistryScanStatus();

        UpdateHeaderHomeostasisButtons();
        RefreshVelumCommandButtons();
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumAgentTaskPane.RefreshFromEngine: " + ex);
        ShowAgentMonitorUiFull();
        ClearMosaic();
        ClearActualProblemField();
      }

      try
      {
        LayoutScrollContents();
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumAgentTaskPane.LayoutScrollContents: " + ex);
      }
    }

    #region Countdown indicator (in-header waiting pulse display)

    /// <summary>
    /// Показать индикатор обратного отсчёта в шапке панели.
    /// Вызывается после успешного исполнения рецепта через RecipeDispatcher.
    /// </summary>
    internal void ShowCountdown(string actionName)
    {
      if (_lblCountdown == null || _lblCountdown.IsDisposed)
        return;

      _lblCountdown.Text = actionName ?? "Action";
      _lblCountdown.Visible = true;
      _countdownColorPulseState = 0;
      _countdownPulseTimer?.Start();
    }

    /// <summary>
    /// Скрыть индикатор обратного отсчёта из шапки панели.
    /// </summary>
    internal void HideCountdown()
    {
      if (_countdownPulseTimer != null)
        _countdownPulseTimer.Stop();

      if (_lblCountdown != null && !_lblCountdown.IsDisposed)
      {
        _lblCountdown.Visible = false;
        _lblCountdown.Text = "…";
        _lblCountdown.ForeColor = Color.FromArgb(0, 112, 192);
      }
    }

    /// <summary>
    /// Обновить состояние индикатора ожидания на основе текущего состояния гомеостаза.
    /// Приоритет: режим наблюдения (таймер B) > ожидание ответа (таймер A) > покой.
    /// </summary>
    private void UpdateCountdownIndicatorState()
    {
      if (_lblCountdown == null || _lblCountdown.IsDisposed)
        return;

      // Приоритет 1: режим наблюдения (таймер B post-motor wait)
      if (ISIDA.Psychic.OperatorMotorObservationSession.IsInitialized &&
          ISIDA.Psychic.OperatorMotorObservationSession.Instance.IsActive)
      {
        int remaining = ISIDA.Psychic.OperatorMotorObservationSession.Instance.GetRemainingPostMotorWaitPulses(
            AppGlobalState.WaitingPeriodForActionsVal);
        _lblCountdown.Text = "Режим наблюдения: ожидание действия оператора. " + remaining;
        _lblCountdown.Visible = true;
        _lblCountdown.ForeColor = Color.FromArgb(0, 112, 192); // Синий
        _countdownColorPulseState = 0;
        _countdownPulseTimer?.Start();
        return;
      }

      // Приоритет 2: ожидание оценки (таймер A WaitingForOperatorEvaluation)
      bool waiting = AppGlobalState.WaitingForOperatorEvaluation && AppGlobalState.WaitingPeriodCountdown > 0;

      if (waiting)
      {
        // Активное ожидание — показываем обратный отсчёт.
        _lblCountdown.Text = "Ожидание оценки: " +
            AppGlobalState.WaitingPeriodCountdown.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _lblCountdown.Visible = true;
        _countdownColorPulseState = 0;
        _countdownPulseTimer?.Start();
      }
      else
      {
        // Нет активного ожидания — показываем серый текст.
        _lblCountdown.Text = "Ожидание оценки: Нет";
        _lblCountdown.Visible = true;
        _lblCountdown.ForeColor = Color.Gray;
        _countdownPulseTimer?.Stop();
      }
    }

    /// <summary>
    /// Обновить отображение обратного отсчёта: пульсация цвета.
    /// Вызывается на каждом тике таймера (1000 мс).
    /// </summary>
    private void UpdateCountdownDisplay()
    {
      if (_lblCountdown == null || _lblCountdown.IsDisposed)
        return;

      try
      {
        if (!AppGlobalState.WaitingForOperatorEvaluation || AppGlobalState.WaitingPeriodCountdown <= 0)
        {
          // Ожидание истекло — остановить таймер.
          _countdownPulseTimer?.Stop();
          return;
        }

        // Пульсация синего цвета (каждый тик = 1 секунда).
        if (_countdownColorPulseState == 1)
        {
          _lblCountdown.ForeColor = Color.FromArgb(0, 112, 192); // Bright blue
          _countdownColorPulseState = 0;
        }
        else
        {
          _lblCountdown.ForeColor = Color.FromArgb(120, 175, 220); // Lighter blue
          _countdownColorPulseState = 1;
        }
      }
      catch
      {
        // Ignore update errors
      }
    }

    /// <summary>
    /// Тик таймера пульсации индикатора — уменьшить обратный отсчёт и обновить цвет.
    /// Вызывается каждые 1000 мс (1 пульс/с, как в AIStudio).
    /// При активном режиме наблюдения (таймер B) уменьшение WaitingPeriodCountdown не выполняется —
    /// таймер сессии зависит от GlobalTimer.GlobalPulsCount.
    /// </summary>
    private void OnCountdownPulseTick(object sender, EventArgs e)
    {
      try
      {
        // При режиме наблюдения (таймер B) — не уменьшаем WaitingPeriodCountdown,
        // таймер сессии считается через GlobalTimer.GlobalPulsCount.
        bool observationActive = ISIDA.Psychic.OperatorMotorObservationSession.IsInitialized &&
            ISIDA.Psychic.OperatorMotorObservationSession.Instance.IsActive;

        if (!observationActive)
        {
          // Уменьшаем обратный отсчёт периода ожидания (таймер A).
          AppGlobalState.UpdateWaitingPeriodCountdown();
        }

        // Обновляем пульсацию цвета.
        UpdateCountdownDisplay();
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumAgentTaskPane countdown tick: " + ex.Message);
      }
    }

    #endregion

    /// <summary>
    /// Пульсация выключена (или ISIDA ещё не поднята): переключатель секции и команды Velum в шапке.
    /// </summary>
    private void ApplyAgentMonitorIdleChrome()
    {
      _agentDeadAwaitReviveChrome = false;
      _agentIdleChrome = true;
      ClearMosaic();
      ClearActualProblemField();
      _lastStateBeforePulse = AppGlobalState.HomeostasisState.Normal;
      if (_txtMessageInput != null)
        _txtMessageInput.Clear();
      if (_txtSwCommandBuffer != null)
        _txtSwCommandBuffer.Clear();
      VelumSolidCommandBuffer.Clear();
      if (_txtAgentOutput != null)
        _txtAgentOutput.Clear();
      if (_scrollPanel != null)
        _scrollPanel.Visible = true;
      if (_scrollHost != null)
        _scrollHost.Visible = true;
      if (_btnHeaderNormHomeostasis != null)
        _btnHeaderNormHomeostasis.Enabled = false;
      if (_btnHeaderReviveAgent != null)
        _btnHeaderReviveAgent.Enabled = false;
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
      {
        _rootLayout.RowStyles[0] = new RowStyle(
            SizeType.Absolute,
            _metricsSectionExpanded ? HeaderPanelHeightPx : 0f);
        _rootLayout.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
      }

      ApplyHeaderChromeForCurrentMode();
      RefreshVelumCommandButtons();
    }

    /// <summary>
    /// Скрывает прокручиваемую область вкладки и шапку (критическая ошибка чтения движка).
    /// </summary>
    private void ApplyAgentMonitorBlank()
    {
      _agentDeadAwaitReviveChrome = false;
      _agentIdleChrome = false;
      ClearMosaic();
      _lastStateBeforePulse = AppGlobalState.HomeostasisState.Normal;
      if (_txtMessageInput != null)
        _txtMessageInput.Clear();
      if (_txtSwCommandBuffer != null)
        _txtSwCommandBuffer.Clear();
      VelumSolidCommandBuffer.Clear();
      if (_txtAgentOutput != null)
        _txtAgentOutput.Clear();
      if (_scrollPanel != null)
        _scrollPanel.Visible = false;
      if (_headerPanel != null)
        _headerPanel.Visible = false;
      if (_scrollHost != null)
        _scrollHost.Visible = false;
      if (_btnHeaderNormHomeostasis != null)
        _btnHeaderNormHomeostasis.Enabled = false;
      if (_btnHeaderReviveAgent != null)
        _btnHeaderReviveAgent.Enabled = false;
      if (_btnHeaderVelumPulseStart != null)
        _btnHeaderVelumPulseStart.Visible = false;
      if (_btnHeaderVelumPulseStop != null)
        _btnHeaderVelumPulseStop.Visible = false;
      if (_btnHeaderVelumProjectSettings != null)
        _btnHeaderVelumProjectSettings.Visible = false;
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
      {
        _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, 0f);
        _rootLayout.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
      }
    }

    /// <summary>
    /// Пульсация включена: шапка состояния + прокручиваемый монитор метрик среды.
    /// </summary>
    private void ShowAgentMonitorUiFull()
    {
      _agentDeadAwaitReviveChrome = false;
      _agentIdleChrome = false;
      if (_scrollPanel != null)
        _scrollPanel.Visible = true;
      if (_scrollHost != null)
        _scrollHost.Visible = true;
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
      {
        _rootLayout.RowStyles[0] = new RowStyle(
            SizeType.Absolute,
            _metricsSectionExpanded ? HeaderPanelHeightPx : 0f);
        _rootLayout.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
      }

      ApplyHeaderChromeForCurrentMode();
      RefreshVelumCommandButtons();
    }

    /// <summary>
    /// Агент мёртв, пульсация остановлена: только шапка с кнопкой «В» (как в AIStudio при остановленной пульсации).
    /// </summary>
    private void ShowAgentDeadAwaitReviveChrome()
    {
      _agentDeadAwaitReviveChrome = true;
      _agentIdleChrome = false;
      if (_scrollPanel != null)
        _scrollPanel.Visible = false;
      if (_scrollHost != null)
        _scrollHost.Visible = false;
      if (_rootLayout != null && _rootLayout.RowCount >= 2)
      {
        _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, HeaderPanelHeightPx);
        _rootLayout.RowStyles[1] = new RowStyle(SizeType.Absolute, 0f);
      }

      ApplyHeaderChromeForCurrentMode();
      RefreshVelumCommandButtons();
    }

    private static string BuildMetricSignature(List<InfluenceActionSystem.GomeostasisInfluenceAction> list)
    {
      if (list == null || list.Count == 0)
        return string.Empty;
      var sb = new StringBuilder(list.Count * 8);
      for (int i = 0; i < list.Count; i++)
      {
        if (i > 0)
          sb.Append(',');
        InfluenceActionSystem.GomeostasisInfluenceAction m = list[i];
        sb.Append(m.Id).Append(':').Append(m.IsActive ? '1' : '0');
      }

      return sb.ToString();
    }

    /// <summary>Индикатор доступности CAD (SessionHealth) в шапке панели.</summary>
    private void ApplyCadEnvironmentStatusLabelIfChanged()
    {
      if (_lblCadEnvironmentStatus == null)
        return;

      string text;
      Color color;
      if (!VelumIsidaHost.IsReady || !GlobalTimer.IsPulsationRunning)
      {
        text = "CAD: —";
        color = Color.DimGray;
      }
      else
      {
        Xarial.XCad.IXApplication app = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        float score = VelumSolidSessionHealth.ComputeScore(app);
        VelumCadDegradedModeSync.SyncFromCurrentSession(app);
        if (AppGlobalState.HostEnvironmentDegraded)
        {
          text = "CAD: без исполнения";
          color = Color.Chocolate;
        }
        else
        {
          text = VelumSolidSessionHealth.FormatUiStatus(score);
          if (score >= VelumSolidSessionHealth.ScoreHealthy - 0.5f)
            color = Color.ForestGreen;
          else if (score >= VelumSolidSessionHealth.ScoreComError - 0.5f)
            color = Color.DarkGoldenrod;
          else if (score >= VelumSolidSessionHealth.ScoreDegraded - 0.5f)
            color = Color.Chocolate;
          else
            color = Color.Firebrick;
        }
      }

      string sig = text + "|" + color.ToArgb();
      if (sig == _cadEnvironmentUiSignature)
        return;
      _cadEnvironmentUiSignature = sig;
      _lblCadEnvironmentStatus.Text = text;
      _lblCadEnvironmentStatus.ForeColor = color;
    }

    private void ApplyOverallStateLabelIfChanged(AppGlobalState.HomeostasisState state)
    {
      string text;
      Color color;
      switch (state)
      {
        case AppGlobalState.HomeostasisState.Bad:
          text = "ПЛОХО";
          color = Color.Firebrick;
          break;
        case AppGlobalState.HomeostasisState.Normal:
          text = "НОРМА";
          color = StateNormalFore;
          break;
        case AppGlobalState.HomeostasisState.Well:
          text = "ХОРОШО";
          color = Color.ForestGreen;
          break;
        default:
          text = "—";
          color = Color.DimGray;
          break;
      }

      ApplyOverallStateLabel(text, color);
    }

    private void ApplyOverallStateLabel(string text, Color color)
    {
      if (_lblStateValue.Text == text && _lblStateValue.ForeColor == color)
        return;
      _lblStateValue.Text = text;
      _lblStateValue.ForeColor = color;
    }

    /// <summary>Иконки и подсказки команд Velum в шапке панели (как на тулбаре).</summary>
    private void ConfigureHeaderVelumCommandButtons()
    {
      ConfigureHeaderVelumCommandButton(
          _btnHeaderVelumPulseStart,
          Resources.PulseStart16,
          "Запустить цикл агента ISIDA с моделью.");
      ConfigureHeaderVelumCommandButton(
          _btnHeaderVelumPulseStop,
          Resources.PulseStop16,
          "Остановить цикл агента ISIDA.");
      ConfigureHeaderVelumCommandButton(
          _btnHeaderVelumProjectSettings,
          Resources.Settings16,
          "Открыть настройки проекта.");
    }

    private void ConfigureHeaderVelumCommandButton(Button btn, Bitmap icon, string toolTip)
    {
      if (btn == null)
        return;
      btn.Image = icon;
      btn.Text = string.Empty;
      btn.ImageAlign = ContentAlignment.MiddleCenter;
      btn.TextImageRelation = TextImageRelation.ImageAboveText;
      btn.TabStop = false;
      if (_parameterToolTip != null)
        _parameterToolTip.SetToolTip(btn, toolTip);
    }

    /// <summary>
    /// Видимость шапки: команды Velum, строка состояния и кнопки Н/В.
    /// </summary>
    private void ApplyHeaderChromeForCurrentMode()
    {
      bool expanded = _metricsSectionExpanded;
      bool showVelumCommands = _agentDeadAwaitReviveChrome || expanded;
      bool showStateHomeo = _agentDeadAwaitReviveChrome || (!_agentIdleChrome && expanded);

      if (_btnHeaderVelumPulseStart != null)
        _btnHeaderVelumPulseStart.Visible = showVelumCommands;
      if (_btnHeaderVelumPulseStop != null)
        _btnHeaderVelumPulseStop.Visible = showVelumCommands;
      if (_btnHeaderVelumProjectSettings != null)
        _btnHeaderVelumProjectSettings.Visible = showVelumCommands;
      if (_flowStateLabels != null)
        _flowStateLabels.Visible = showStateHomeo;
      if (_btnHeaderNormHomeostasis != null)
        _btnHeaderNormHomeostasis.Visible = showStateHomeo;
      if (_btnHeaderReviveAgent != null)
        _btnHeaderReviveAgent.Visible = showStateHomeo;

      if (_headerPanel != null)
        _headerPanel.Visible = showVelumCommands || showStateHomeo;
    }

    /// <summary>Обновляет доступность кнопок Старт/Стоп/Настройки (как у команд тулбара Velum).</summary>
    internal void RefreshVelumCommandButtons()
    {
      bool ready = VelumIsidaHost.IsReady;
      bool running = false;
      bool dead = false;
      try
      {
        if (ready)
        {
          running = GlobalTimer.IsPulsationRunning;
          dead = AppGlobalState.IsDead;
        }
      }
      catch
      {
      }

      bool isAdmin = VelumAdminAccess.IsAdmin;
      bool hasUntracked = VelumSolidProbeRefreshPlanner.HasUntrackedChanges;

      if (_btnHeaderVelumPulseStart != null)
      {
        _btnHeaderVelumPulseStart.Enabled = isAdmin && !running && (!ready || !dead);
        // Подсказка с предупреждением о непроверенных изменениях
        string startTip = "Запустить цикл агента ISIDA с моделью.";
        if (hasUntracked && !running)
          startTip += " (обнаружены изменения, накопленные вне пульса)";
        if (_parameterToolTip != null)
          _parameterToolTip.SetToolTip(_btnHeaderVelumPulseStart, startTip);
      }
      if (_btnHeaderVelumPulseStop != null)
        _btnHeaderVelumPulseStop.Enabled = ready && running;
      if (_btnHeaderVelumProjectSettings != null)
        _btnHeaderVelumProjectSettings.Enabled = isAdmin;
    }

    private void OnHeaderVelumPulseStartClick(object sender, EventArgs e)
    {
      if (_btnHeaderVelumPulseStart != null && !_btnHeaderVelumPulseStart.Enabled)
        return;
      VelumPulseStartRequested?.Invoke();
    }

    private void OnHeaderVelumPulseStopClick(object sender, EventArgs e)
    {
      if (_btnHeaderVelumPulseStop != null && !_btnHeaderVelumPulseStop.Enabled)
        return;
      VelumPulseStopRequested?.Invoke();
    }

    private void OnHeaderVelumProjectSettingsClick(object sender, EventArgs e)
    {
      VelumProjectSettingsRequested?.Invoke();
    }

    /// <summary>
    /// «Н» — сброс в норму при живом агенте, в т.ч. при включённой пульсации (панель команд; в AIStudio на странице агента кнопка «Норма» при пульсе отключена).
    /// «В» — только при <c>IsDead</c> и остановленной пульсации; визуально «серое» и курсор «нет», подсказка на отключённой WinForms-кнопке не работает — кнопка остаётся Enabled для ToolTip, клик блокируется флагом.
    /// </summary>
    private void UpdateHeaderHomeostasisButtons()
    {
      if (_btnHeaderNormHomeostasis == null || _btnHeaderReviveAgent == null)
        return;
      if (_headerPanel == null || !_headerPanel.Visible)
        return;
      if (!_btnHeaderNormHomeostasis.Visible && !_btnHeaderReviveAgent.Visible)
        return;
      if (!VelumIsidaHost.IsReady)
      {
        _headerReviveInvocationAllowed = false;
        _btnHeaderNormHomeostasis.Enabled = false;
        _btnHeaderReviveAgent.Enabled = false;
        _btnHeaderNormHomeostasis.TabStop = false;
        _btnHeaderReviveAgent.TabStop = false;
        return;
      }

      bool dead = AppGlobalState.IsDead;
      bool pulse = GlobalTimer.IsPulsationRunning;

      bool normCanClick = !dead;
      _btnHeaderNormHomeostasis.Enabled = normCanClick;
      _btnHeaderNormHomeostasis.TabStop = false;
      if (normCanClick)
      {
        _btnHeaderNormHomeostasis.BackColor = HeaderNormOnBack;
        _btnHeaderNormHomeostasis.ForeColor = HeaderNormOnFore;
        _btnHeaderNormHomeostasis.FlatAppearance.BorderColor = HeaderNormOnBorder;
        _btnHeaderNormHomeostasis.Cursor = Cursors.Hand;
      }
      else
      {
        _btnHeaderNormHomeostasis.BackColor = HeaderHomeoOffBack;
        _btnHeaderNormHomeostasis.ForeColor = HeaderHomeoOffFore;
        _btnHeaderNormHomeostasis.FlatAppearance.BorderColor = HeaderHomeoOffBorder;
        _btnHeaderNormHomeostasis.Cursor = Cursors.No;
      }

      _headerReviveInvocationAllowed = dead && !pulse;
      _btnHeaderReviveAgent.Enabled = true;
      _btnHeaderReviveAgent.TabStop = false;
      if (_headerReviveInvocationAllowed)
      {
        _btnHeaderReviveAgent.BackColor = HeaderReviveOnBack;
        _btnHeaderReviveAgent.ForeColor = HeaderReviveOnFore;
        _btnHeaderReviveAgent.FlatAppearance.BorderColor = HeaderReviveOnBorder;
        _btnHeaderReviveAgent.Cursor = Cursors.Hand;
      }
      else
      {
        _btnHeaderReviveAgent.BackColor = HeaderHomeoOffBack;
        _btnHeaderReviveAgent.ForeColor = HeaderHomeoOffFore;
        _btnHeaderReviveAgent.FlatAppearance.BorderColor = HeaderHomeoOffBorder;
        _btnHeaderReviveAgent.Cursor = Cursors.No;
      }

      RefreshHeaderHomeostasisTooltips();
    }

    private void RefreshHeaderHomeostasisTooltips()
    {
      if (_parameterToolTip == null)
        return;
      if (_btnHeaderNormHomeostasis != null)
      {
        _parameterToolTip.SetToolTip(
            _btnHeaderNormHomeostasis,
            "Установить значения параметров в состояние НОРМА");
      }

      if (_btnHeaderReviveAgent != null)
      {
        _parameterToolTip.SetToolTip(
            _btnHeaderReviveAgent,
            "Снять IsDead, сохранить свойства и привести параметры к НОРМА (только если агент мёртв и пульсация остановлена).");
      }
    }

    private void OnHeaderNormHomeostasisClick(object sender, EventArgs e)
    {
      if (!VelumIsidaHost.IsReady || AppGlobalState.IsDead)
        return;
      try
      {
        GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;
        g.ApplySpeedOrientedNormalHomeostasisForScenarioPreRun();
        VelumSolidMetricPressureReset.OnManualNormHomeostasis();
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            ex.Message,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      GomeostasSystem gomeo = VelumIsidaHost.Context.Gomeostas;
      var (propsOk, propsErr) = gomeo.SaveAgentProperties();
      if (!propsOk)
      {
        MessageBox.Show(
            "Не удалось сохранить свойства агента:\n" + propsErr,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      var (paramsOk, paramsErr) = gomeo.SaveAgentParameters();
      if (!paramsOk)
      {
        MessageBox.Show(
            "Не удалось сохранить значения параметров:\n" + paramsErr,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      RequestRefresh();
    }

    private void OnHeaderReviveAgentClick(object sender, EventArgs e)
    {
      if (!VelumIsidaHost.IsReady || !_headerReviveInvocationAllowed)
        return;

      GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;
      GomeostasSystem.AgentStateInfo agentInfo = g.GetAgentState();
      string name = agentInfo?.Name?.Trim();
      if (string.IsNullOrEmpty(name))
        name = "агент";

      if (MessageBox.Show(
              "Воскресить агента «" + name + "»?\nБудет снята отметка смерти, параметры приведутся к состоянию «Норма».",
              "Воскрешение агента",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      var (reviveOk, reviveErr) = g.ReviveAgentSaveProperties();
      if (!reviveOk)
      {
        MessageBox.Show(
            "Не удалось сохранить свойства агента:\n" + reviveErr,
            "Воскрешение агента",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      try
      {
        g.ApplySpeedOrientedNormalHomeostasisForScenarioPreRun();
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            ex.Message,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        RequestRefresh();
        return;
      }

      var (propsOk, propsErr) = g.SaveAgentProperties();
      if (!propsOk)
      {
        MessageBox.Show(
            "Не удалось сохранить свойства агента:\n" + propsErr,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      var (paramsOk, paramsErr) = g.SaveAgentParameters();
      if (!paramsOk)
      {
        MessageBox.Show(
            "Не удалось сохранить значения параметров:\n" + paramsErr,
            "НОРМА",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      RequestRefresh();
    }

    /// <summary>
    /// Enter в поле ввода — дубль кнопки «Отправить» (перехват на уровне WndProc контрола ввода).
    /// </summary>
    private void OnSendMessageClick(object sender, EventArgs e)
    {
      if (_txtMessageInput == null || _txtAgentOutput == null)
        return;

      string rawMsg = _txtMessageInput.Text ?? string.Empty;
      string sanitized = VelumOperatorStimulusCodec.SanitizeOperatorMessage(rawMsg);
      string commandPreview = VelumSolidCommandBuffer.PeekSnapshot();
      var (verbalPreview, commandPreviewLine) = VelumOperatorStimulusCodec.ParseOperatorInput(sanitized, commandPreview);

      List<int> influenceSnapshot = _operatorInfluenceIds
          .Where(id => id > 0)
          .Distinct()
          .ToList();

      if (string.IsNullOrWhiteSpace(verbalPreview) &&
          string.IsNullOrWhiteSpace(commandPreviewLine) &&
          influenceSnapshot.Count == 0)
      {
        MessageBox.Show(
            "Нет текста для отправки, буфер команд SolidWorks пуст и не выбраны воздействия на параметры.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      QueueVerbalPrimariesToBufferIfAutoAdd(sanitized);
      VelumCommandIdleFlusher.CancelIdleTimer();
      string commandSnapshot = VelumSolidCommandBuffer.ConsumeSnapshot();
      var (verbalLine, commandLine) = VelumOperatorStimulusCodec.ParseOperatorInput(sanitized, commandSnapshot);
      if (IsAutoAddSensorsEnabled())
        QueueCommandPrimariesToBufferIfAutoAddFromLine(commandLine);

      if (!string.IsNullOrWhiteSpace(verbalLine) || !string.IsNullOrWhiteSpace(commandLine))
      {
        string display = string.Join(" ", new[] { verbalLine, commandLine }.Where(s => !string.IsNullOrWhiteSpace(s)));
        AppendAgentOutputLine("Вы: " + display);
      }
      else
      {
        string directLine = _txtOperatorInfluencesDisplay != null ? _txtOperatorInfluencesDisplay.Text : string.Empty;
        AppendAgentOutputLine(
            "Вы: " +
            (string.IsNullOrWhiteSpace(directLine) ? "воздействия на параметры агента" : directLine));
      }

      _txtMessageInput.Clear();

      try
      {
        if (!VelumIsidaHost.TryInitialize(out string initErr))
        {
          AppendAgentOutputLine("Ошибка: " + initErr);
          return;
        }

        if (!GlobalTimer.IsPulsationRunning)
        {
          AppendAgentOutputLine("Пульсация выключена — стимул не применён.");
          return;
        }

        if (AppGlobalState.IsDead)
        {
          AppendAgentOutputLine("Агент мёртв — стимул не применён.");
          return;
        }

        if (!VelumAgentStimulusSender.TrySendOperatorStimulus(
                verbalLine ?? string.Empty,
                commandLine ?? string.Empty,
                influenceSnapshot,
                0,
                0,
                out string sendErr))
        {
          AppendAgentOutputLine("Ошибка: " + (sendErr ?? "неизвестная ошибка"));
          return;
        }

        _operatorInfluenceIds.Clear();
        RefreshOperatorInfluencesDisplay();
        AppendAgentOutputLine("Стимул применён (речь + команды → ISIDA).");
      }
      finally
      {
        RefreshSwBufferText();
      }
    }

    private void OnSwBufferClearClick(object sender, EventArgs e)
    {
      VelumCommandIdleFlusher.CancelIdleTimer();
      VelumSolidCommandBuffer.Clear();
      RefreshSwBufferText();
    }

    private void OnSolidCommandBufferChanged()
    {
      RefreshSwBufferText();
      QueueCommandPrimariesToBufferIfAutoAdd();
    }

    private void RefreshSwBufferText()
    {
      if (_txtSwCommandBuffer == null || _txtSwCommandBuffer.IsDisposed)
        return;
      try
      {
        _txtSwCommandBuffer.Text = VelumSolidCommandBuffer.GetSnapshot();
      }
      catch
      {
      }
    }

    private void AppendAgentOutputLine(string line)
    {
      if (_txtAgentOutput == null)
        return;
      if (_txtAgentOutput.TextLength > 0)
        _txtAgentOutput.AppendText(Environment.NewLine);
      _txtAgentOutput.AppendText(line);
      _txtAgentOutput.SelectionStart = _txtAgentOutput.Text.Length;
      _txtAgentOutput.ScrollToCaret();
    }
  }
}
