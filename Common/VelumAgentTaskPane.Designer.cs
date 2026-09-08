namespace Velum.UI
{
  /// <summary>Сгенерированная разметка панели задач «Агент».</summary>
  public sealed partial class VelumAgentTaskPane
  {
    private System.ComponentModel.IContainer components = null;

    /// <summary>Очистка ресурсов.</summary>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
        components.Dispose();
      base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._headerPanel = new System.Windows.Forms.Panel();
      this._tblHeaderLayout = new System.Windows.Forms.TableLayoutPanel();
      this._flowStateLabels = new System.Windows.Forms.FlowLayoutPanel();
      this._lblStatePrefix = new System.Windows.Forms.Label();
      this._lblStateValue = new System.Windows.Forms.Label();
      this._lblCadEnvironmentStatus = new System.Windows.Forms.Label();
      this._lblCountdown = new System.Windows.Forms.Label();
      this._lblRegistryScanStatus = new System.Windows.Forms.Label();
      this._pnlHeaderCenterSpacer = new System.Windows.Forms.Panel();
      this._flowHeaderHomeoButtons = new System.Windows.Forms.FlowLayoutPanel();
      this._btnHeaderVelumPulseStart = new System.Windows.Forms.Button();
      this._btnHeaderVelumPulseStop = new System.Windows.Forms.Button();
      this._btnHeaderVelumProjectSettings = new System.Windows.Forms.Button();
      this._btnHeaderNormHomeostasis = new System.Windows.Forms.Button();
      this._btnHeaderReviveAgent = new System.Windows.Forms.Button();
      this._scrollPanel = new System.Windows.Forms.Panel();
      this._scrollHost = new System.Windows.Forms.Panel();
      this._btnToggleParamsAndStyles = new System.Windows.Forms.Button();
      this._pnlParametersCaptionRow = new System.Windows.Forms.Panel();
      this._lblParametersCaption = new System.Windows.Forms.Label();
      this._btnMetricsSettingsPick = new System.Windows.Forms.Button();
      this._pnlActualProblemRow = new System.Windows.Forms.Panel();
      this._lblActualProblemCaption = new System.Windows.Forms.Label();
      this._lblActualProblemValue = new System.Windows.Forms.Label();
      this._brickGrid = new System.Windows.Forms.TableLayoutPanel();
      this._flowSwBufferCaption = new System.Windows.Forms.FlowLayoutPanel();
      this._lblSwBufferCaption = new System.Windows.Forms.Label();
      this._lblSwBufferAuthoritativeHint = new System.Windows.Forms.Label();
      this._pnlSwBufferButtonsRow = new System.Windows.Forms.Panel();
      this._btnViewCommandPrimariesBuffer = new System.Windows.Forms.Button();
      this._btnSwBufferClear = new System.Windows.Forms.Button();
      this._txtSwCommandBuffer = new System.Windows.Forms.TextBox();
      this._tblSwBufferRow = new System.Windows.Forms.TableLayoutPanel();
      this._flowSwBufferChecks = new System.Windows.Forms.FlowLayoutPanel();
      this._chkAutoAddSensors = new System.Windows.Forms.CheckBox();
      this._chkVerbalAuthoritative = new System.Windows.Forms.CheckBox();
      this._pnlSwBufferMiddleSpacer = new System.Windows.Forms.Panel();
      this._lblOperatorInfluencesCaption = new System.Windows.Forms.Label();
      this._pnlOperatorInfluences = new System.Windows.Forms.Panel();
      this._txtOperatorInfluencesDisplay = new System.Windows.Forms.TextBox();
      this._btnOperatorInfluencesPick = new System.Windows.Forms.Button();
      this._flowInputCaption = new System.Windows.Forms.FlowLayoutPanel();
      this._lblInputCaption = new System.Windows.Forms.Label();
      this._lblInputAuthoritativeHint = new System.Windows.Forms.Label();
      this._btnViewVerbalPrimariesBuffer = new System.Windows.Forms.Button();
      this._txtMessageInput = new System.Windows.Forms.TextBox();
      this._pnlSendRow = new System.Windows.Forms.Panel();
      this._btnSend = new System.Windows.Forms.Button();
      this._lblOutputCaption = new System.Windows.Forms.Label();
      this._txtAgentOutput = new System.Windows.Forms.TextBox();
      this._parameterToolTip = new System.Windows.Forms.ToolTip(this.components);
      this._rootLayout.SuspendLayout();
      this._headerPanel.SuspendLayout();
      this._tblHeaderLayout.SuspendLayout();
      this._flowStateLabels.SuspendLayout();
      this._flowHeaderHomeoButtons.SuspendLayout();
      this._scrollPanel.SuspendLayout();
      this._scrollHost.SuspendLayout();
      this._pnlParametersCaptionRow.SuspendLayout();
      this._pnlActualProblemRow.SuspendLayout();
      this._flowSwBufferCaption.SuspendLayout();
      this._pnlSwBufferButtonsRow.SuspendLayout();
      this._tblSwBufferRow.SuspendLayout();
      this._flowSwBufferChecks.SuspendLayout();
      this._pnlOperatorInfluences.SuspendLayout();
      this._flowInputCaption.SuspendLayout();
      this._pnlSendRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._headerPanel, 0, 0);
      this._rootLayout.Controls.Add(this._scrollPanel, 0, 1);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Margin = new System.Windows.Forms.Padding(0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.RowCount = 2;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Size = new System.Drawing.Size(300, 400);
      this._rootLayout.TabIndex = 0;
      // 
      // _headerPanel
      // 
      this._headerPanel.Controls.Add(this._tblHeaderLayout);
      this._headerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._headerPanel.Location = new System.Drawing.Point(0, 0);
      this._headerPanel.Margin = new System.Windows.Forms.Padding(0);
      this._headerPanel.Name = "_headerPanel";
      this._headerPanel.Padding = new System.Windows.Forms.Padding(2, 1, 2, 1);
      this._headerPanel.Size = new System.Drawing.Size(300, 56);
      this._headerPanel.TabIndex = 0;
      // 
      // _tblHeaderLayout
      // 
      this._tblHeaderLayout.ColumnCount = 4;
      this._tblHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._tblHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._tblHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._tblHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._tblHeaderLayout.Controls.Add(this._flowStateLabels, 0, 0);
      this._tblHeaderLayout.Controls.Add(this._pnlHeaderCenterSpacer, 1, 0);
      this._tblHeaderLayout.Controls.Add(this._flowHeaderHomeoButtons, 2, 0);
      this._tblHeaderLayout.Controls.Add(this._lblCountdown, 0, 1);
      this._tblHeaderLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._tblHeaderLayout.Location = new System.Drawing.Point(2, 1);
      this._tblHeaderLayout.Margin = new System.Windows.Forms.Padding(0);
      this._tblHeaderLayout.Name = "_tblHeaderLayout";
      this._tblHeaderLayout.RowCount = 2;
      this._tblHeaderLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._tblHeaderLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
      this._tblHeaderLayout.Size = new System.Drawing.Size(296, 52);
      this._tblHeaderLayout.TabIndex = 0;
      // 
      // _flowStateLabels
      // 
      this._flowStateLabels.AutoSize = true;
      this._flowStateLabels.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowStateLabels.Controls.Add(this._lblStatePrefix);
      this._flowStateLabels.Controls.Add(this._lblStateValue);
      this._flowStateLabels.Controls.Add(this._lblCadEnvironmentStatus);
      this._flowStateLabels.Dock = System.Windows.Forms.DockStyle.Fill;
      this._flowStateLabels.Location = new System.Drawing.Point(0, 0);
      this._flowStateLabels.Margin = new System.Windows.Forms.Padding(0);
      this._flowStateLabels.Name = "_flowStateLabels";
      this._flowStateLabels.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
      this._flowStateLabels.Size = new System.Drawing.Size(137, 30);
      this._flowStateLabels.TabIndex = 0;
      this._flowStateLabels.WrapContents = false;
      // 
      // _lblStatePrefix
      // 
      this._lblStatePrefix.AutoSize = true;
      this._lblStatePrefix.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblStatePrefix.Location = new System.Drawing.Point(0, 4);
      this._lblStatePrefix.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
      this._lblStatePrefix.Name = "_lblStatePrefix";
      this._lblStatePrefix.Size = new System.Drawing.Size(69, 15);
      this._lblStatePrefix.TabIndex = 0;
      this._lblStatePrefix.Text = "Состояние:";
      // 
      // _lblStateValue
      // 
      this._lblStateValue.AutoSize = true;
      this._lblStateValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblStateValue.Location = new System.Drawing.Point(72, 4);
      this._lblStateValue.Margin = new System.Windows.Forms.Padding(0);
      this._lblStateValue.Name = "_lblStateValue";
      this._lblStateValue.Size = new System.Drawing.Size(19, 15);
      this._lblStateValue.TabIndex = 1;
      this._lblStateValue.Text = "—";
      // 
      // _lblCadEnvironmentStatus
      this._lblCadEnvironmentStatus.AutoSize = true;
      this._lblCadEnvironmentStatus.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblCadEnvironmentStatus.ForeColor = System.Drawing.Color.DimGray;
      this._lblCadEnvironmentStatus.Location = new System.Drawing.Point(91, 6);
      this._lblCadEnvironmentStatus.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
      this._lblCadEnvironmentStatus.Name = "_lblCadEnvironmentStatus";
      this._lblCadEnvironmentStatus.Size = new System.Drawing.Size(46, 13);
      this._lblCadEnvironmentStatus.TabIndex = 2;
      this._lblCadEnvironmentStatus.Text = "CAD: —";
      // 
      // _lblCountdown
      // 
      this._lblCountdown.AutoSize = true;
      this._lblCountdown.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblCountdown.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(112)))), ((int)(((byte)(192)))));
      this._lblCountdown.Location = new System.Drawing.Point(4, 32);
      this._lblCountdown.Margin = new System.Windows.Forms.Padding(4, 0, 0, 0);
      this._lblCountdown.Name = "_lblCountdown";
      this._lblCountdown.Size = new System.Drawing.Size(13, 13);
      this._lblCountdown.TabIndex = 4;
      this._lblCountdown.Text = "…";
      this._lblCountdown.Visible = false;
      this._tblHeaderLayout.SetColumnSpan(this._lblCountdown, 4);
      // 
      // _lblRegistryScanStatus
      // 
      this._lblRegistryScanStatus.AutoSize = true;
      this._lblRegistryScanStatus.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblRegistryScanStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(112)))), ((int)(((byte)(192)))));
      this._lblRegistryScanStatus.Location = new System.Drawing.Point(6, 30);
      this._lblRegistryScanStatus.Name = "_lblRegistryScanStatus";
      this._lblRegistryScanStatus.Size = new System.Drawing.Size(116, 13);
      this._lblRegistryScanStatus.TabIndex = 31;
      this._lblRegistryScanStatus.Text = "Сканирование...";
      this._parameterToolTip.SetToolTip(this._lblRegistryScanStatus, "Статус фонового сканирования реестра. № — номер итерации, в скобках — периодичность сканирования в пульсах.");
      this._lblRegistryScanStatus.Visible = false;
      // 
      // _pnlHeaderCenterSpacer
      // 
      this._pnlHeaderCenterSpacer.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pnlHeaderCenterSpacer.Location = new System.Drawing.Point(137, 0);
      this._pnlHeaderCenterSpacer.Margin = new System.Windows.Forms.Padding(0);
      this._pnlHeaderCenterSpacer.Name = "_pnlHeaderCenterSpacer";
      this._pnlHeaderCenterSpacer.Size = new System.Drawing.Size(31, 30);
      this._pnlHeaderCenterSpacer.TabIndex = 1;
      // 
      // _flowHeaderHomeoButtons
      // 
      this._flowHeaderHomeoButtons.AutoSize = true;
      this._flowHeaderHomeoButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowHeaderHomeoButtons.Controls.Add(this._btnHeaderVelumPulseStart);
      this._flowHeaderHomeoButtons.Controls.Add(this._btnHeaderVelumPulseStop);
      this._flowHeaderHomeoButtons.Controls.Add(this._btnHeaderVelumProjectSettings);
      this._flowHeaderHomeoButtons.Controls.Add(this._btnHeaderNormHomeostasis);
      this._flowHeaderHomeoButtons.Controls.Add(this._btnHeaderReviveAgent);
      this._flowHeaderHomeoButtons.Dock = System.Windows.Forms.DockStyle.Fill;
      this._flowHeaderHomeoButtons.Location = new System.Drawing.Point(168, 0);
      this._flowHeaderHomeoButtons.Margin = new System.Windows.Forms.Padding(0);
      this._flowHeaderHomeoButtons.Name = "_flowHeaderHomeoButtons";
      this._flowHeaderHomeoButtons.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
      this._flowHeaderHomeoButtons.Size = new System.Drawing.Size(128, 30);
      this._flowHeaderHomeoButtons.TabIndex = 2;
      this._flowHeaderHomeoButtons.WrapContents = false;
      // 
      // _btnHeaderVelumPulseStart
      // 
      this._btnHeaderVelumPulseStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnHeaderVelumPulseStart.Location = new System.Drawing.Point(0, 2);
      this._btnHeaderVelumPulseStart.Margin = new System.Windows.Forms.Padding(0, 0, 2, 0);
      this._btnHeaderVelumPulseStart.Name = "_btnHeaderVelumPulseStart";
      this._btnHeaderVelumPulseStart.Size = new System.Drawing.Size(24, 24);
      this._btnHeaderVelumPulseStart.TabIndex = 0;
      this._btnHeaderVelumPulseStart.UseVisualStyleBackColor = true;
      // 
      // _btnHeaderVelumPulseStop
      // 
      this._btnHeaderVelumPulseStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnHeaderVelumPulseStop.Location = new System.Drawing.Point(26, 2);
      this._btnHeaderVelumPulseStop.Margin = new System.Windows.Forms.Padding(0, 0, 2, 0);
      this._btnHeaderVelumPulseStop.Name = "_btnHeaderVelumPulseStop";
      this._btnHeaderVelumPulseStop.Size = new System.Drawing.Size(24, 24);
      this._btnHeaderVelumPulseStop.TabIndex = 1;
      this._btnHeaderVelumPulseStop.UseVisualStyleBackColor = true;
      // 
      // _btnHeaderVelumProjectSettings
      // 
      this._btnHeaderVelumProjectSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnHeaderVelumProjectSettings.Location = new System.Drawing.Point(52, 2);
      this._btnHeaderVelumProjectSettings.Margin = new System.Windows.Forms.Padding(0, 0, 2, 0);
      this._btnHeaderVelumProjectSettings.Name = "_btnHeaderVelumProjectSettings";
      this._btnHeaderVelumProjectSettings.Size = new System.Drawing.Size(24, 24);
      this._btnHeaderVelumProjectSettings.TabIndex = 2;
      this._btnHeaderVelumProjectSettings.UseVisualStyleBackColor = true;
      // 
      // _btnHeaderNormHomeostasis
      // 
      this._btnHeaderNormHomeostasis.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(224)))), ((int)(((byte)(130)))));
      this._btnHeaderNormHomeostasis.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(160)))), ((int)(((byte)(23)))));
      this._btnHeaderNormHomeostasis.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnHeaderNormHomeostasis.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnHeaderNormHomeostasis.ForeColor = System.Drawing.Color.Black;
      this._btnHeaderNormHomeostasis.Location = new System.Drawing.Point(78, 2);
      this._btnHeaderNormHomeostasis.Margin = new System.Windows.Forms.Padding(0, 0, 2, 0);
      this._btnHeaderNormHomeostasis.Name = "_btnHeaderNormHomeostasis";
      this._btnHeaderNormHomeostasis.Size = new System.Drawing.Size(24, 24);
      this._btnHeaderNormHomeostasis.TabIndex = 0;
      this._btnHeaderNormHomeostasis.Text = "Н";
      this._btnHeaderNormHomeostasis.UseVisualStyleBackColor = false;
      // 
      // _btnHeaderReviveAgent
      // 
      this._btnHeaderReviveAgent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
      this._btnHeaderReviveAgent.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(125)))), ((int)(((byte)(50)))));
      this._btnHeaderReviveAgent.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnHeaderReviveAgent.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnHeaderReviveAgent.ForeColor = System.Drawing.Color.White;
      this._btnHeaderReviveAgent.Location = new System.Drawing.Point(104, 2);
      this._btnHeaderReviveAgent.Margin = new System.Windows.Forms.Padding(0);
      this._btnHeaderReviveAgent.Name = "_btnHeaderReviveAgent";
      this._btnHeaderReviveAgent.Size = new System.Drawing.Size(24, 24);
      this._btnHeaderReviveAgent.TabIndex = 1;
      this._btnHeaderReviveAgent.Text = "В";
      this._btnHeaderReviveAgent.UseVisualStyleBackColor = false;
      // 
      // _scrollPanel
      // 
      this._scrollPanel.AutoScroll = true;
      this._scrollPanel.Controls.Add(this._scrollHost);
      this._scrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._scrollPanel.Location = new System.Drawing.Point(0, 32);
      this._scrollPanel.Margin = new System.Windows.Forms.Padding(0);
      this._scrollPanel.Name = "_scrollPanel";
      this._scrollPanel.Size = new System.Drawing.Size(300, 368);
      this._scrollPanel.TabIndex = 1;
      // 
      // _scrollHost
      // 
      this._scrollHost.Controls.Add(this._btnToggleParamsAndStyles);
      this._scrollHost.Controls.Add(this._lblRegistryScanStatus);
      this._scrollHost.Controls.Add(this._pnlParametersCaptionRow);
      this._scrollHost.Controls.Add(this._brickGrid);
      this._scrollHost.Controls.Add(this._pnlActualProblemRow);
      this._scrollHost.Controls.Add(this._flowSwBufferCaption);
      this._scrollHost.Controls.Add(this._pnlSwBufferButtonsRow);
      this._scrollHost.Controls.Add(this._txtSwCommandBuffer);
      this._scrollHost.Controls.Add(this._tblSwBufferRow);
      this._scrollHost.Controls.Add(this._lblOperatorInfluencesCaption);
      this._scrollHost.Controls.Add(this._pnlOperatorInfluences);
      this._scrollHost.Controls.Add(this._flowInputCaption);
      this._scrollHost.Controls.Add(this._btnViewVerbalPrimariesBuffer);
      this._scrollHost.Controls.Add(this._txtMessageInput);
      this._scrollHost.Controls.Add(this._pnlSendRow);
      this._scrollHost.Controls.Add(this._lblOutputCaption);
      this._scrollHost.Controls.Add(this._txtAgentOutput);
      this._scrollHost.Location = new System.Drawing.Point(0, 0);
      this._scrollHost.Margin = new System.Windows.Forms.Padding(0);
      this._scrollHost.Name = "_scrollHost";
      this._scrollHost.Size = new System.Drawing.Size(300, 400);
      this._scrollHost.TabIndex = 0;
      // 
      // _btnToggleParamsAndStyles
      // 
      this._btnToggleParamsAndStyles.Cursor = System.Windows.Forms.Cursors.Hand;
      this._btnToggleParamsAndStyles.FlatAppearance.BorderSize = 0;
      this._btnToggleParamsAndStyles.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnToggleParamsAndStyles.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnToggleParamsAndStyles.Location = new System.Drawing.Point(2, 2);
      this._btnToggleParamsAndStyles.Margin = new System.Windows.Forms.Padding(0);
      this._btnToggleParamsAndStyles.Name = "_btnToggleParamsAndStyles";
      this._btnToggleParamsAndStyles.Size = new System.Drawing.Size(296, 26);
      this._btnToggleParamsAndStyles.TabIndex = 30;
      this._btnToggleParamsAndStyles.TabStop = false;
      this._btnToggleParamsAndStyles.Text = "▼ Состояние и метрики среды агента";
      this._btnToggleParamsAndStyles.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      this._btnToggleParamsAndStyles.UseVisualStyleBackColor = true;
      // 
      // _pnlParametersCaptionRow
      // 
      this._pnlParametersCaptionRow.Controls.Add(this._lblParametersCaption);
      this._pnlParametersCaptionRow.Controls.Add(this._btnMetricsSettingsPick);
      this._pnlParametersCaptionRow.Location = new System.Drawing.Point(6, 50);
      this._pnlParametersCaptionRow.Margin = new System.Windows.Forms.Padding(0);
      this._pnlParametersCaptionRow.Name = "_pnlParametersCaptionRow";
      this._pnlParametersCaptionRow.Size = new System.Drawing.Size(288, 24);
      this._pnlParametersCaptionRow.TabIndex = 31;
      // 
      // _lblParametersCaption
      // 
      this._lblParametersCaption.AutoSize = true;
      this._lblParametersCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblParametersCaption.Location = new System.Drawing.Point(0, 4);
      this._lblParametersCaption.Margin = new System.Windows.Forms.Padding(0);
      this._lblParametersCaption.Name = "_lblParametersCaption";
      this._lblParametersCaption.Size = new System.Drawing.Size(96, 15);
      this._lblParametersCaption.TabIndex = 0;
      this._lblParametersCaption.Text = "Метрики среды:";
      // 
      // _btnMetricsSettingsPick
      // 
      this._btnMetricsSettingsPick.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this._btnMetricsSettingsPick.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnMetricsSettingsPick.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnMetricsSettingsPick.Location = new System.Drawing.Point(262, 0);
      this._btnMetricsSettingsPick.Margin = new System.Windows.Forms.Padding(0);
      this._btnMetricsSettingsPick.Name = "_btnMetricsSettingsPick";
      this._btnMetricsSettingsPick.Size = new System.Drawing.Size(24, 24);
      this._btnMetricsSettingsPick.TabIndex = 1;
      this._btnMetricsSettingsPick.Text = "…";
      this._parameterToolTip.SetToolTip(this._btnMetricsSettingsPick, "Включение и выключение метрик среды");
      this._btnMetricsSettingsPick.UseVisualStyleBackColor = true;
      // 
      // _brickGrid
      // 
      this._brickGrid.ColumnCount = 1;
      this._brickGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 6F));
      this._brickGrid.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
      this._brickGrid.Location = new System.Drawing.Point(6, 68);
      this._brickGrid.Margin = new System.Windows.Forms.Padding(0);
      this._brickGrid.Name = "_brickGrid";
      this._brickGrid.Padding = new System.Windows.Forms.Padding(1);
      this._brickGrid.RowCount = 1;
      this._brickGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 6F));
      this._brickGrid.Size = new System.Drawing.Size(8, 8);
      this._brickGrid.TabIndex = 1;
      // 
      // _pnlActualProblemRow
      // 
      this._pnlActualProblemRow.Controls.Add(this._lblActualProblemCaption);
      this._pnlActualProblemRow.Controls.Add(this._lblActualProblemValue);
      this._pnlActualProblemRow.Location = new System.Drawing.Point(6, 86);
      this._pnlActualProblemRow.Margin = new System.Windows.Forms.Padding(0);
      this._pnlActualProblemRow.Name = "_pnlActualProblemRow";
      this._pnlActualProblemRow.Size = new System.Drawing.Size(288, 18);
      this._pnlActualProblemRow.TabIndex = 32;
      // 
      // _lblActualProblemCaption
      // 
      this._lblActualProblemCaption.AutoSize = true;
      this._lblActualProblemCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblActualProblemCaption.Location = new System.Drawing.Point(0, 1);
      this._lblActualProblemCaption.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblActualProblemCaption.Name = "_lblActualProblemCaption";
      this._lblActualProblemCaption.Size = new System.Drawing.Size(128, 15);
      this._lblActualProblemCaption.TabIndex = 0;
      this._lblActualProblemCaption.Text = "Актуальная проблема:";
      // 
      // _lblActualProblemValue
      // 
      this._lblActualProblemValue.AutoSize = true;
      this._lblActualProblemValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblActualProblemValue.Location = new System.Drawing.Point(134, 1);
      this._lblActualProblemValue.Margin = new System.Windows.Forms.Padding(0);
      this._lblActualProblemValue.Name = "_lblActualProblemValue";
      this._lblActualProblemValue.Size = new System.Drawing.Size(0, 15);
      this._lblActualProblemValue.TabIndex = 1;
      this._lblActualProblemValue.Text = "";
      // 
      // _flowSwBufferCaption
      // 
      this._flowSwBufferCaption.AutoSize = true;
      this._flowSwBufferCaption.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowSwBufferCaption.Controls.Add(this._lblSwBufferCaption);
      this._flowSwBufferCaption.Controls.Add(this._lblSwBufferAuthoritativeHint);
      this._flowSwBufferCaption.Location = new System.Drawing.Point(6, 100);
      this._flowSwBufferCaption.Margin = new System.Windows.Forms.Padding(0);
      this._flowSwBufferCaption.Name = "_flowSwBufferCaption";
      this._flowSwBufferCaption.Size = new System.Drawing.Size(310, 15);
      this._flowSwBufferCaption.TabIndex = 20;
      this._flowSwBufferCaption.WrapContents = false;
      // 
      // _lblSwBufferCaption
      // 
      this._lblSwBufferCaption.AutoSize = true;
      this._lblSwBufferCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblSwBufferCaption.Location = new System.Drawing.Point(0, 0);
      this._lblSwBufferCaption.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblSwBufferCaption.Name = "_lblSwBufferCaption";
      this._lblSwBufferCaption.Size = new System.Drawing.Size(188, 15);
      this._lblSwBufferCaption.TabIndex = 0;
      this._lblSwBufferCaption.Text = "Буфер команд SolidWorks (sw:…):";
      // 
      // _lblSwBufferAuthoritativeHint
      // 
      this._lblSwBufferAuthoritativeHint.AutoSize = true;
      this._lblSwBufferAuthoritativeHint.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblSwBufferAuthoritativeHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(125)))), ((int)(((byte)(50)))));
      this._lblSwBufferAuthoritativeHint.Location = new System.Drawing.Point(194, 0);
      this._lblSwBufferAuthoritativeHint.Margin = new System.Windows.Forms.Padding(0);
      this._lblSwBufferAuthoritativeHint.Name = "_lblSwBufferAuthoritativeHint";
      this._lblSwBufferAuthoritativeHint.Size = new System.Drawing.Size(116, 13);
      this._lblSwBufferAuthoritativeHint.TabIndex = 1;
      this._lblSwBufferAuthoritativeHint.Text = "сбор новых токенов";
      this._lblSwBufferAuthoritativeHint.Visible = false;
      // 
      // _pnlSwBufferButtonsRow
      // 
      this._pnlSwBufferButtonsRow.Controls.Add(this._btnViewCommandPrimariesBuffer);
      this._pnlSwBufferButtonsRow.Controls.Add(this._btnSwBufferClear);
      this._pnlSwBufferButtonsRow.Location = new System.Drawing.Point(6, 118);
      this._pnlSwBufferButtonsRow.Margin = new System.Windows.Forms.Padding(0);
      this._pnlSwBufferButtonsRow.Name = "_pnlSwBufferButtonsRow";
      this._pnlSwBufferButtonsRow.Size = new System.Drawing.Size(288, 24);
      this._pnlSwBufferButtonsRow.TabIndex = 32;
      // 
      // _btnViewCommandPrimariesBuffer
      // 
      this._btnViewCommandPrimariesBuffer.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnViewCommandPrimariesBuffer.Location = new System.Drawing.Point(0, 0);
      this._btnViewCommandPrimariesBuffer.Margin = new System.Windows.Forms.Padding(0, 0, 0, 2);
      this._btnViewCommandPrimariesBuffer.Name = "_btnViewCommandPrimariesBuffer";
      this._btnViewCommandPrimariesBuffer.Size = new System.Drawing.Size(50, 23);
      this._btnViewCommandPrimariesBuffer.TabIndex = 0;
      this._btnViewCommandPrimariesBuffer.Text = "Буфер";
      this._parameterToolTip.SetToolTip(this._btnViewCommandPrimariesBuffer, "Новые сенсоры");
      this._btnViewCommandPrimariesBuffer.UseVisualStyleBackColor = true;
      // 
      // _btnSwBufferClear
      // 
      this._btnSwBufferClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this._btnSwBufferClear.AutoSize = true;
      this._btnSwBufferClear.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnSwBufferClear.Location = new System.Drawing.Point(217, 0);
      this._btnSwBufferClear.Margin = new System.Windows.Forms.Padding(0);
      this._btnSwBufferClear.Name = "_btnSwBufferClear";
      this._btnSwBufferClear.Size = new System.Drawing.Size(71, 23);
      this._btnSwBufferClear.TabIndex = 1;
      this._btnSwBufferClear.Text = "Очистить";
      this._btnSwBufferClear.UseVisualStyleBackColor = true;
      // 
      // _txtSwCommandBuffer
      // 
      this._txtSwCommandBuffer.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._txtSwCommandBuffer.Location = new System.Drawing.Point(6, 118);
      this._txtSwCommandBuffer.Margin = new System.Windows.Forms.Padding(0);
      this._txtSwCommandBuffer.Multiline = true;
      this._txtSwCommandBuffer.Name = "_txtSwCommandBuffer";
      this._txtSwCommandBuffer.ReadOnly = true;
      this._txtSwCommandBuffer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._txtSwCommandBuffer.Size = new System.Drawing.Size(288, 40);
      this._txtSwCommandBuffer.TabIndex = 21;
      // 
      // _tblSwBufferRow
      // 
      this._tblSwBufferRow.ColumnCount = 2;
      this._tblSwBufferRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._tblSwBufferRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._tblSwBufferRow.Controls.Add(this._flowSwBufferChecks, 0, 0);
      this._tblSwBufferRow.Controls.Add(this._pnlSwBufferMiddleSpacer, 1, 0);
      this._tblSwBufferRow.Location = new System.Drawing.Point(6, 162);
      this._tblSwBufferRow.Margin = new System.Windows.Forms.Padding(0);
      this._tblSwBufferRow.Name = "_tblSwBufferRow";
      this._tblSwBufferRow.RowCount = 1;
      this._tblSwBufferRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._tblSwBufferRow.Size = new System.Drawing.Size(288, 28);
      this._tblSwBufferRow.TabIndex = 22;
      // 
      // _flowSwBufferChecks
      // 
      this._flowSwBufferChecks.AutoSize = true;
      this._flowSwBufferChecks.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowSwBufferChecks.Controls.Add(this._chkAutoAddSensors);
      this._flowSwBufferChecks.Controls.Add(this._chkVerbalAuthoritative);
      this._flowSwBufferChecks.Dock = System.Windows.Forms.DockStyle.Fill;
      this._flowSwBufferChecks.Location = new System.Drawing.Point(0, 0);
      this._flowSwBufferChecks.Margin = new System.Windows.Forms.Padding(0);
      this._flowSwBufferChecks.Name = "_flowSwBufferChecks";
      this._flowSwBufferChecks.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
      this._flowSwBufferChecks.Size = new System.Drawing.Size(302, 28);
      this._flowSwBufferChecks.TabIndex = 0;
      this._flowSwBufferChecks.WrapContents = false;
      // 
      // _chkAutoAddSensors
      // 
      this._chkAutoAddSensors.AutoSize = true;
      this._chkAutoAddSensors.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._chkAutoAddSensors.Location = new System.Drawing.Point(3, 5);
      this._chkAutoAddSensors.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
      this._chkAutoAddSensors.Name = "_chkAutoAddSensors";
      this._chkAutoAddSensors.Size = new System.Drawing.Size(143, 17);
      this._chkAutoAddSensors.TabIndex = 0;
      this._chkAutoAddSensors.Text = "Сбор новых сенсоров";
      this._chkAutoAddSensors.UseVisualStyleBackColor = true;
      // 
      // _chkVerbalAuthoritative
      // 
      this._chkVerbalAuthoritative.AutoSize = true;
      this._chkVerbalAuthoritative.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._chkVerbalAuthoritative.Location = new System.Drawing.Point(155, 5);
      this._chkVerbalAuthoritative.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
      this._chkVerbalAuthoritative.Name = "_chkVerbalAuthoritative";
      this._chkVerbalAuthoritative.Size = new System.Drawing.Size(141, 17);
      this._chkVerbalAuthoritative.TabIndex = 1;
      this._chkVerbalAuthoritative.Text = "Авторитарная запись";
      this._chkVerbalAuthoritative.UseVisualStyleBackColor = true;
      // 
      // _pnlSwBufferMiddleSpacer
      // 
      this._pnlSwBufferMiddleSpacer.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pnlSwBufferMiddleSpacer.Location = new System.Drawing.Point(302, 0);
      this._pnlSwBufferMiddleSpacer.Margin = new System.Windows.Forms.Padding(0);
      this._pnlSwBufferMiddleSpacer.Name = "_pnlSwBufferMiddleSpacer";
      this._pnlSwBufferMiddleSpacer.Size = new System.Drawing.Size(1, 28);
      this._pnlSwBufferMiddleSpacer.TabIndex = 1;
      // 
      // _lblOperatorInfluencesCaption
      // 
      this._lblOperatorInfluencesCaption.AutoSize = true;
      this._lblOperatorInfluencesCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblOperatorInfluencesCaption.Location = new System.Drawing.Point(6, 194);
      this._lblOperatorInfluencesCaption.Margin = new System.Windows.Forms.Padding(0);
      this._lblOperatorInfluencesCaption.Name = "_lblOperatorInfluencesCaption";
      this._lblOperatorInfluencesCaption.Size = new System.Drawing.Size(124, 15);
      this._lblOperatorInfluencesCaption.TabIndex = 23;
      this._lblOperatorInfluencesCaption.Text = "Прямое воздействие:";
      // 
      // _pnlOperatorInfluences
      // 
      this._pnlOperatorInfluences.Controls.Add(this._txtOperatorInfluencesDisplay);
      this._pnlOperatorInfluences.Controls.Add(this._btnOperatorInfluencesPick);
      this._pnlOperatorInfluences.Location = new System.Drawing.Point(6, 212);
      this._pnlOperatorInfluences.Margin = new System.Windows.Forms.Padding(0);
      this._pnlOperatorInfluences.Name = "_pnlOperatorInfluences";
      this._pnlOperatorInfluences.Size = new System.Drawing.Size(288, 28);
      this._pnlOperatorInfluences.TabIndex = 24;
      // 
      // _txtOperatorInfluencesDisplay
      // 
      this._txtOperatorInfluencesDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this._txtOperatorInfluencesDisplay.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._txtOperatorInfluencesDisplay.Location = new System.Drawing.Point(0, 2);
      this._txtOperatorInfluencesDisplay.Margin = new System.Windows.Forms.Padding(0);
      this._txtOperatorInfluencesDisplay.Name = "_txtOperatorInfluencesDisplay";
      this._txtOperatorInfluencesDisplay.ReadOnly = true;
      this._txtOperatorInfluencesDisplay.Size = new System.Drawing.Size(256, 22);
      this._txtOperatorInfluencesDisplay.TabIndex = 0;
      // 
      // _btnOperatorInfluencesPick
      // 
      this._btnOperatorInfluencesPick.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this._btnOperatorInfluencesPick.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._btnOperatorInfluencesPick.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnOperatorInfluencesPick.Location = new System.Drawing.Point(262, 0);
      this._btnOperatorInfluencesPick.Margin = new System.Windows.Forms.Padding(0);
      this._btnOperatorInfluencesPick.Name = "_btnOperatorInfluencesPick";
      this._btnOperatorInfluencesPick.Size = new System.Drawing.Size(24, 24);
      this._btnOperatorInfluencesPick.TabIndex = 1;
      this._btnOperatorInfluencesPick.Text = "…";
      this._btnOperatorInfluencesPick.UseVisualStyleBackColor = true;
      // 
      // _flowInputCaption
      // 
      this._flowInputCaption.AutoSize = true;
      this._flowInputCaption.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowInputCaption.Controls.Add(this._lblInputCaption);
      this._flowInputCaption.Controls.Add(this._lblInputAuthoritativeHint);
      this._flowInputCaption.Location = new System.Drawing.Point(6, 244);
      this._flowInputCaption.Margin = new System.Windows.Forms.Padding(0);
      this._flowInputCaption.Name = "_flowInputCaption";
      this._flowInputCaption.Size = new System.Drawing.Size(272, 15);
      this._flowInputCaption.TabIndex = 4;
      this._flowInputCaption.WrapContents = false;
      // 
      // _lblInputCaption
      // 
      this._lblInputCaption.AutoSize = true;
      this._lblInputCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblInputCaption.Location = new System.Drawing.Point(0, 0);
      this._lblInputCaption.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblInputCaption.Name = "_lblInputCaption";
      this._lblInputCaption.Size = new System.Drawing.Size(104, 15);
      this._lblInputCaption.TabIndex = 0;
      this._lblInputCaption.Text = "Ввод сообщений:";
      // 
      // _lblInputAuthoritativeHint
      // 
      this._lblInputAuthoritativeHint.AutoSize = true;
      this._lblInputAuthoritativeHint.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblInputAuthoritativeHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(125)))), ((int)(((byte)(50)))));
      this._lblInputAuthoritativeHint.Location = new System.Drawing.Point(110, 0);
      this._lblInputAuthoritativeHint.Margin = new System.Windows.Forms.Padding(0);
      this._lblInputAuthoritativeHint.Name = "_lblInputAuthoritativeHint";
      this._lblInputAuthoritativeHint.Size = new System.Drawing.Size(162, 13);
      this._lblInputAuthoritativeHint.TabIndex = 1;
      this._lblInputAuthoritativeHint.Text = "автодобавление слов и фраз";
      this._lblInputAuthoritativeHint.Visible = false;
      // 
      // _btnViewVerbalPrimariesBuffer
      // 
      this._btnViewVerbalPrimariesBuffer.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnViewVerbalPrimariesBuffer.Location = new System.Drawing.Point(6, 264);
      this._btnViewVerbalPrimariesBuffer.Margin = new System.Windows.Forms.Padding(0, 0, 0, 2);
      this._btnViewVerbalPrimariesBuffer.Name = "_btnViewVerbalPrimariesBuffer";
      this._btnViewVerbalPrimariesBuffer.Size = new System.Drawing.Size(50, 23);
      this._btnViewVerbalPrimariesBuffer.TabIndex = 5;
      this._btnViewVerbalPrimariesBuffer.Text = "Буфер";
      this._parameterToolTip.SetToolTip(this._btnViewVerbalPrimariesBuffer, "Новые сенсоры");
      this._btnViewVerbalPrimariesBuffer.UseVisualStyleBackColor = true;
      // 
      // _txtMessageInput
      // 
      this._txtMessageInput.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._txtMessageInput.Location = new System.Drawing.Point(6, 264);
      this._txtMessageInput.Margin = new System.Windows.Forms.Padding(0);
      this._txtMessageInput.Multiline = true;
      this._txtMessageInput.Name = "_txtMessageInput";
      this._txtMessageInput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._txtMessageInput.Size = new System.Drawing.Size(288, 56);
      this._txtMessageInput.TabIndex = 5;
      // 
      // _pnlSendRow
      // 
      this._pnlSendRow.Controls.Add(this._btnSend);
      this._pnlSendRow.Location = new System.Drawing.Point(6, 322);
      this._pnlSendRow.Margin = new System.Windows.Forms.Padding(0);
      this._pnlSendRow.Name = "_pnlSendRow";
      this._pnlSendRow.Size = new System.Drawing.Size(288, 30);
      this._pnlSendRow.TabIndex = 25;
      // 
      // _btnSend
      // 
      this._btnSend.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._btnSend.Location = new System.Drawing.Point(2, 2);
      this._btnSend.Margin = new System.Windows.Forms.Padding(0);
      this._btnSend.Name = "_btnSend";
      this._btnSend.Size = new System.Drawing.Size(284, 24);
      this._btnSend.TabIndex = 0;
      this._btnSend.Text = "Отправить";
      this._btnSend.UseVisualStyleBackColor = true;
      // 
      // _lblOutputCaption
      // 
      this._lblOutputCaption.AutoSize = true;
      this._lblOutputCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblOutputCaption.Location = new System.Drawing.Point(6, 220);
      this._lblOutputCaption.Margin = new System.Windows.Forms.Padding(0);
      this._lblOutputCaption.Name = "_lblOutputCaption";
      this._lblOutputCaption.Size = new System.Drawing.Size(79, 15);
      this._lblOutputCaption.TabIndex = 7;
      this._lblOutputCaption.Text = "Ответ агента:";
      // 
      // _txtAgentOutput
      // 
      this._txtAgentOutput.BackColor = System.Drawing.SystemColors.Window;
      this._txtAgentOutput.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._txtAgentOutput.Location = new System.Drawing.Point(6, 240);
      this._txtAgentOutput.Margin = new System.Windows.Forms.Padding(0);
      this._txtAgentOutput.Multiline = true;
      this._txtAgentOutput.Name = "_txtAgentOutput";
      this._txtAgentOutput.ReadOnly = true;
      this._txtAgentOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._txtAgentOutput.Size = new System.Drawing.Size(288, 120);
      this._txtAgentOutput.TabIndex = 8;
      // 
      // _parameterToolTip
      // 
      this._parameterToolTip.ShowAlways = true;
      // 
      // VelumAgentTaskPane
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.Controls.Add(this._rootLayout);
      this.Name = "VelumAgentTaskPane";
      this.Size = new System.Drawing.Size(300, 400);
      this._rootLayout.ResumeLayout(false);
      this._headerPanel.ResumeLayout(false);
      this._tblHeaderLayout.ResumeLayout(false);
      this._tblHeaderLayout.PerformLayout();
      this._flowStateLabels.ResumeLayout(false);
      this._flowStateLabels.PerformLayout();
      this._flowHeaderHomeoButtons.ResumeLayout(false);
      this._scrollPanel.ResumeLayout(false);
      this._scrollHost.ResumeLayout(false);
      this._scrollHost.PerformLayout();
      this._pnlParametersCaptionRow.ResumeLayout(false);
      this._pnlParametersCaptionRow.PerformLayout();
      this._pnlActualProblemRow.ResumeLayout(false);
      this._pnlActualProblemRow.PerformLayout();
      this._flowSwBufferCaption.ResumeLayout(false);
      this._flowSwBufferCaption.PerformLayout();
      this._pnlSwBufferButtonsRow.ResumeLayout(false);
      this._pnlSwBufferButtonsRow.PerformLayout();
      this._tblSwBufferRow.ResumeLayout(false);
      this._tblSwBufferRow.PerformLayout();
      this._flowSwBufferChecks.ResumeLayout(false);
      this._flowSwBufferChecks.PerformLayout();
      this._pnlOperatorInfluences.ResumeLayout(false);
      this._pnlOperatorInfluences.PerformLayout();
      this._flowInputCaption.ResumeLayout(false);
      this._flowInputCaption.PerformLayout();
      this._pnlSendRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.Panel _headerPanel;
    private System.Windows.Forms.TableLayoutPanel _tblHeaderLayout;
    private System.Windows.Forms.FlowLayoutPanel _flowStateLabels;
    private System.Windows.Forms.Label _lblStatePrefix;
    private System.Windows.Forms.Label _lblStateValue;
    private System.Windows.Forms.Label _lblCadEnvironmentStatus;
    private System.Windows.Forms.Label _lblCountdown;
    private System.Windows.Forms.Label _lblRegistryScanStatus;
    private System.Windows.Forms.Panel _pnlHeaderCenterSpacer;
    private System.Windows.Forms.FlowLayoutPanel _flowHeaderHomeoButtons;
    private System.Windows.Forms.Button _btnHeaderVelumPulseStart;
    private System.Windows.Forms.Button _btnHeaderVelumPulseStop;
    private System.Windows.Forms.Button _btnHeaderVelumProjectSettings;
    private System.Windows.Forms.Button _btnHeaderNormHomeostasis;
    private System.Windows.Forms.Button _btnHeaderReviveAgent;
    private System.Windows.Forms.Panel _scrollPanel;
    private System.Windows.Forms.Panel _scrollHost;
    private System.Windows.Forms.Button _btnToggleParamsAndStyles;
    private System.Windows.Forms.Panel _pnlParametersCaptionRow;
    private System.Windows.Forms.Label _lblParametersCaption;
    private System.Windows.Forms.Button _btnMetricsSettingsPick;
    private System.Windows.Forms.TableLayoutPanel _brickGrid;
    private System.Windows.Forms.Panel _pnlActualProblemRow;
    private System.Windows.Forms.Label _lblActualProblemCaption;
    private System.Windows.Forms.Label _lblActualProblemValue;
    private System.Windows.Forms.Label _lblSwBufferCaption;
    private System.Windows.Forms.FlowLayoutPanel _flowSwBufferCaption;
    private System.Windows.Forms.Label _lblSwBufferAuthoritativeHint;
    private System.Windows.Forms.Panel _pnlSwBufferButtonsRow;
    private System.Windows.Forms.Button _btnViewCommandPrimariesBuffer;
    private System.Windows.Forms.TextBox _txtSwCommandBuffer;
    private System.Windows.Forms.TableLayoutPanel _tblSwBufferRow;
    private System.Windows.Forms.FlowLayoutPanel _flowSwBufferChecks;
    private System.Windows.Forms.CheckBox _chkAutoAddSensors;
    private System.Windows.Forms.CheckBox _chkVerbalAuthoritative;
    private System.Windows.Forms.Panel _pnlSwBufferMiddleSpacer;
    private System.Windows.Forms.Button _btnSwBufferClear;
    private System.Windows.Forms.Label _lblOperatorInfluencesCaption;
    private System.Windows.Forms.Panel _pnlOperatorInfluences;
    private System.Windows.Forms.TextBox _txtOperatorInfluencesDisplay;
    private System.Windows.Forms.Button _btnOperatorInfluencesPick;
    private System.Windows.Forms.Label _lblInputCaption;
    private System.Windows.Forms.FlowLayoutPanel _flowInputCaption;
    private System.Windows.Forms.Label _lblInputAuthoritativeHint;
    private System.Windows.Forms.Button _btnViewVerbalPrimariesBuffer;
    private System.Windows.Forms.TextBox _txtMessageInput;
    private System.Windows.Forms.Panel _pnlSendRow;
    private System.Windows.Forms.Button _btnSend;
    private System.Windows.Forms.Label _lblOutputCaption;
    private System.Windows.Forms.TextBox _txtAgentOutput;
    private System.Windows.Forms.ToolTip _parameterToolTip;
  }
}
