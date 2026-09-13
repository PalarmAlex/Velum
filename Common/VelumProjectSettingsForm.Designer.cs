using System.Drawing;

namespace Velum.UI
{
  public sealed partial class VelumProjectSettingsForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label _lblStage2SearchPlayStyleIds;
    private System.Windows.Forms.TextBox _tbStage2SearchPlayStyleIds;
    private System.Windows.Forms.Label _lblBomExchange;
    private System.Windows.Forms.TextBox _tbBomExchange;
    private System.Windows.Forms.Button _btnBrowseBomExchange;

    private System.Windows.Forms.CheckBox _chkNeedDxfDefault;
    private System.Windows.Forms.CheckBox _chkNeedPdfDefault;
    private System.Windows.Forms.CheckBox _chkNeedDrawingDefault;
    private System.Windows.Forms.Label _lblDocumentColorsHint;
    private System.Windows.Forms.Label _lblDocumentColorPart;
    private System.Windows.Forms.TextBox _tbDocumentColorPart;
    private System.Windows.Forms.Label _lblDocumentColorAssembly;
    private System.Windows.Forms.TextBox _tbDocumentColorAssembly;
    private System.Windows.Forms.Label _lblDocumentColorDrawing;
    private System.Windows.Forms.TextBox _tbDocumentColorDrawing;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProjectSettingsForm));
      this.tabControl1 = new System.Windows.Forms.TabControl();
      this.tabPage1 = new System.Windows.Forms.TabPage();
      this._lblPathProductRegistry = new System.Windows.Forms.Label();
      this._tbProductRegistry = new System.Windows.Forms.TextBox();
      this._btnBrowseProductRegistry = new System.Windows.Forms.Button();
      this._lblBomExchange = new System.Windows.Forms.Label();
      this._tbBomExchange = new System.Windows.Forms.TextBox();
      this._btnBrowseBomExchange = new System.Windows.Forms.Button();
      this._lblPathScenario = new System.Windows.Forms.Label();
      this._tbScenarioReports = new System.Windows.Forms.TextBox();
      this._btnBrowseScenario = new System.Windows.Forms.Button();
      this._lblPathBoot = new System.Windows.Forms.Label();
      this._tbBoot = new System.Windows.Forms.TextBox();
      this._btnBrowseBoot = new System.Windows.Forms.Button();
      this._lblPathLogs = new System.Windows.Forms.Label();
      this._tbLogs = new System.Windows.Forms.TextBox();
      this._btnBrowseLogs = new System.Windows.Forms.Button();
      this._lblPathGomeostas = new System.Windows.Forms.Label();
      this._tbGomeostas = new System.Windows.Forms.TextBox();
      this._btnBrowseGomeostas = new System.Windows.Forms.Button();
      this._lblPathSettings = new System.Windows.Forms.Label();
      this._tbSettingsPath = new System.Windows.Forms.TextBox();
      this._btnBrowseSettings = new System.Windows.Forms.Button();
      this.tabPage2 = new System.Windows.Forms.TabPage();
      this._chkVerbalAuthoritative = new System.Windows.Forms.CheckBox();
      this._chkObservationMode = new System.Windows.Forms.CheckBox();
      this._lblLogFormat = new System.Windows.Forms.Label();
      this._cmbLogFormat = new System.Windows.Forms.ComboBox();
      this._lblLogEnabled = new System.Windows.Forms.Label();
       this._chkLog = new System.Windows.Forms.CheckBox();
       this._lblSolidHomeostasisDebugLog = new System.Windows.Forms.Label();
       this._chkSolidHomeostasisDebugLog = new System.Windows.Forms.CheckBox();
       this._lblSolidHostMinDelta = new System.Windows.Forms.Label();
      this._tbSolidHostMinDelta = new System.Windows.Forms.TextBox();
      this._lblSolidMetricEpsilon = new System.Windows.Forms.Label();
      this._tbSolidMetricEpsilon = new System.Windows.Forms.TextBox();
      this._lblRecognition = new System.Windows.Forms.Label();
      this._tbRecognition = new System.Windows.Forms.TextBox();
      this._lblReflexDur = new System.Windows.Forms.Label();
      this._tbReflexDur = new System.Windows.Forms.TextBox();
      this._lblDynamic = new System.Windows.Forms.Label();
      this._tbDynamic = new System.Windows.Forms.TextBox();
      this._lblDifSensor = new System.Windows.Forms.Label();
      this._tbDifSensor = new System.Windows.Forms.TextBox();
      this._lblHomeostasisPulseDrift = new System.Windows.Forms.Label();
      this._chkHomeostasisPulseDrift = new System.Windows.Forms.CheckBox();
      this._lblCompare = new System.Windows.Forms.Label();
      this._tbCompare = new System.Windows.Forms.TextBox();
      this._lblAdaptive = new System.Windows.Forms.Label();
      this._cmbAdaptive = new System.Windows.Forms.ComboBox();
      this._lblStyle = new System.Windows.Forms.Label();
      this._cmbStyle = new System.Windows.Forms.ComboBox();
      this.tabPage3 = new System.Windows.Forms.TabPage();
      this._chkFirstRun = new System.Windows.Forms.CheckBox();
      this._tbDefaultGeneticReflexId = new System.Windows.Forms.TextBox();
      this._lblDefaultGeneticReflexId = new System.Windows.Forms.Label();
      this._lblSilence = new System.Windows.Forms.Label();
      this._tbSilence = new System.Windows.Forms.TextBox();
      this._lblMainMaxAge = new System.Windows.Forms.Label();
      this._tbMainMaxAge = new System.Windows.Forms.TextBox();
      this._lblCycleBase = new System.Windows.Forms.Label();
      this._tbCycleBase = new System.Windows.Forms.TextBox();
      this._lblCycleDiv = new System.Windows.Forms.Label();
      this._tbCycleDiv = new System.Windows.Forms.TextBox();
      this._lblWait = new System.Windows.Forms.Label();
      this._tbWaitOperator = new System.Windows.Forms.TextBox();
      this._lblTheme = new System.Windows.Forms.Label();
      this._cmbTheme = new System.Windows.Forms.ComboBox();
      this.tabPageAdapter = new System.Windows.Forms.TabPage();
      this._lblHeavyMetricsPulsePeriod = new System.Windows.Forms.Label();
      this._tbHeavyMetricsPulsePeriod = new System.Windows.Forms.TextBox();
      this._lblCommandBufferFlushHint = new System.Windows.Forms.Label();
      this._tbCommandBufferMaxAgeSec = new System.Windows.Forms.TextBox();
      this._lblCommandBufferMaxAgeSec = new System.Windows.Forms.Label();
      this._tbCommandBufferMaxTokens = new System.Windows.Forms.TextBox();
      this._lblCommandBufferMaxTokens = new System.Windows.Forms.Label();
      this._tbCommandBufferIdleFlushSec = new System.Windows.Forms.TextBox();
      this._lblCommandBufferIdleFlushSec = new System.Windows.Forms.Label();
      this._lblCommandBufferRecordingHint = new System.Windows.Forms.Label();
      this._chkCommandBufferRecording = new System.Windows.Forms.CheckBox();
      this.tabPageDocuments = new System.Windows.Forms.TabPage();
      this._chkNeedDrawingDefault = new System.Windows.Forms.CheckBox();
      this._chkNeedPdfDefault = new System.Windows.Forms.CheckBox();
      this._chkNeedDxfDefault = new System.Windows.Forms.CheckBox();
      this._lblDocumentColorsHint = new System.Windows.Forms.Label();
      this._lblDocumentColorPart = new System.Windows.Forms.Label();
      this._tbDocumentColorPart = new System.Windows.Forms.TextBox();
      this._lblDocumentColorAssembly = new System.Windows.Forms.Label();
      this._tbDocumentColorAssembly = new System.Windows.Forms.TextBox();
      this._lblDocumentColorDrawing = new System.Windows.Forms.Label();
      this._tbDocumentColorDrawing = new System.Windows.Forms.TextBox();
      this._lblStage2SearchPlayStyleIds = new System.Windows.Forms.Label();
      this._tbStage2SearchPlayStyleIds = new System.Windows.Forms.TextBox();
      this._btnCancel = new System.Windows.Forms.Button();
      this._btnSave = new System.Windows.Forms.Button();
      this._lblStageEvolution = new System.Windows.Forms.Label();
      this.stage_evolution = new System.Windows.Forms.ComboBox();
      this._ttpStageEvolution = new System.Windows.Forms.ToolTip(this.components);
      this._pnlStageBusyOverlay = new System.Windows.Forms.Panel();
      this._lblStageBusy = new System.Windows.Forms.Label();
      this.tabControl1.SuspendLayout();
      this.tabPage1.SuspendLayout();
      this.tabPage2.SuspendLayout();
      this.tabPage3.SuspendLayout();
      this.tabPageAdapter.SuspendLayout();
      this.tabPageDocuments.SuspendLayout();
      this._pnlStageBusyOverlay.SuspendLayout();
      this.SuspendLayout();
      // 
      // tabControl1
      // 
      this.tabControl1.Controls.Add(this.tabPage1);
      this.tabControl1.Controls.Add(this.tabPage2);
      this.tabControl1.Controls.Add(this.tabPage3);
      this.tabControl1.Controls.Add(this.tabPageAdapter);
      this.tabControl1.Controls.Add(this.tabPageDocuments);
      this.tabControl1.Location = new System.Drawing.Point(12, 12);
      this.tabControl1.Name = "tabControl1";
      this.tabControl1.SelectedIndex = 0;
      this.tabControl1.Size = new System.Drawing.Size(684, 318);
      this.tabControl1.TabIndex = 0;
      // 
      // tabPage1
      // 
      this.tabPage1.Controls.Add(this._lblPathProductRegistry);
      this.tabPage1.Controls.Add(this._tbProductRegistry);
      this.tabPage1.Controls.Add(this._btnBrowseProductRegistry);
      this.tabPage1.Controls.Add(this._lblBomExchange);
      this.tabPage1.Controls.Add(this._tbBomExchange);
      this.tabPage1.Controls.Add(this._btnBrowseBomExchange);
      this.tabPage1.Controls.Add(this._lblPathScenario);
      this.tabPage1.Controls.Add(this._tbScenarioReports);
      this.tabPage1.Controls.Add(this._btnBrowseScenario);
      this.tabPage1.Controls.Add(this._lblPathBoot);
      this.tabPage1.Controls.Add(this._tbBoot);
      this.tabPage1.Controls.Add(this._btnBrowseBoot);
      this.tabPage1.Controls.Add(this._lblPathLogs);
      this.tabPage1.Controls.Add(this._tbLogs);
      this.tabPage1.Controls.Add(this._btnBrowseLogs);
      this.tabPage1.Controls.Add(this._lblPathGomeostas);
      this.tabPage1.Controls.Add(this._tbGomeostas);
      this.tabPage1.Controls.Add(this._btnBrowseGomeostas);
      this.tabPage1.Controls.Add(this._lblPathSettings);
      this.tabPage1.Controls.Add(this._tbSettingsPath);
      this.tabPage1.Controls.Add(this._btnBrowseSettings);
      this.tabPage1.Location = new System.Drawing.Point(4, 22);
      this.tabPage1.Name = "tabPage1";
      this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
      this.tabPage1.Size = new System.Drawing.Size(676, 292);
      this.tabPage1.TabIndex = 0;
      this.tabPage1.Text = "Пути данных";
      this.tabPage1.UseVisualStyleBackColor = true;
      // 
      // _lblPathProductRegistry
      // 
      this._lblPathProductRegistry.Location = new System.Drawing.Point(3, 148);
      this._lblPathProductRegistry.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathProductRegistry.Name = "_lblPathProductRegistry";
      this._lblPathProductRegistry.Size = new System.Drawing.Size(175, 20);
      this._lblPathProductRegistry.TabIndex = 31;
      this._lblPathProductRegistry.Text = "Реестр документов:";
      this._lblPathProductRegistry.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbProductRegistry
      // 
      this._tbProductRegistry.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbProductRegistry.Location = new System.Drawing.Point(181, 149);
      this._tbProductRegistry.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbProductRegistry.Name = "_tbProductRegistry";
      this._tbProductRegistry.ReadOnly = true;
      this._tbProductRegistry.Size = new System.Drawing.Size(400, 20);
      this._tbProductRegistry.TabIndex = 32;
      // 
      // _btnBrowseProductRegistry
      // 
      this._btnBrowseProductRegistry.Location = new System.Drawing.Point(590, 148);
      this._btnBrowseProductRegistry.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseProductRegistry.Name = "_btnBrowseProductRegistry";
      this._btnBrowseProductRegistry.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseProductRegistry.TabIndex = 33;
      this._btnBrowseProductRegistry.Tag = this._tbProductRegistry;
      this._btnBrowseProductRegistry.Text = "Обзор...";
      this._btnBrowseProductRegistry.UseVisualStyleBackColor = true;
      this._btnBrowseProductRegistry.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblBomExchange
      // 
      this._lblBomExchange.Location = new System.Drawing.Point(3, 176);
      this._lblBomExchange.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblBomExchange.Name = "_lblBomExchange";
      this._lblBomExchange.Size = new System.Drawing.Size(175, 20);
      this._lblBomExchange.TabIndex = 34;
      this._lblBomExchange.Text = "Каталог обмена с 1С:";
      this._lblBomExchange.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbBomExchange
      // 
      this._tbBomExchange.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbBomExchange.Location = new System.Drawing.Point(181, 177);
      this._tbBomExchange.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbBomExchange.Name = "_tbBomExchange";
      this._tbBomExchange.ReadOnly = true;
      this._tbBomExchange.Size = new System.Drawing.Size(400, 20);
      this._tbBomExchange.TabIndex = 35;
      // 
      // _btnBrowseBomExchange
      // 
      this._btnBrowseBomExchange.Location = new System.Drawing.Point(590, 176);
      this._btnBrowseBomExchange.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseBomExchange.Name = "_btnBrowseBomExchange";
      this._btnBrowseBomExchange.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseBomExchange.TabIndex = 36;
      this._btnBrowseBomExchange.Tag = this._tbBomExchange;
      this._btnBrowseBomExchange.Text = "Обзор...";
      this._btnBrowseBomExchange.UseVisualStyleBackColor = true;
      this._btnBrowseBomExchange.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblPathScenario
      // 
      this._lblPathScenario.Location = new System.Drawing.Point(3, 120);
      this._lblPathScenario.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathScenario.Name = "_lblPathScenario";
      this._lblPathScenario.Size = new System.Drawing.Size(175, 20);
      this._lblPathScenario.TabIndex = 28;
      this._lblPathScenario.Text = "Отчёты сценариев (HTML):";
      this._lblPathScenario.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbScenarioReports
      // 
      this._tbScenarioReports.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbScenarioReports.Location = new System.Drawing.Point(181, 121);
      this._tbScenarioReports.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbScenarioReports.Name = "_tbScenarioReports";
      this._tbScenarioReports.ReadOnly = true;
      this._tbScenarioReports.Size = new System.Drawing.Size(400, 20);
      this._tbScenarioReports.TabIndex = 29;
      // 
      // _btnBrowseScenario
      // 
      this._btnBrowseScenario.Location = new System.Drawing.Point(590, 120);
      this._btnBrowseScenario.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseScenario.Name = "_btnBrowseScenario";
      this._btnBrowseScenario.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseScenario.TabIndex = 30;
      this._btnBrowseScenario.Tag = this._tbScenarioReports;
      this._btnBrowseScenario.Text = "Обзор...";
      this._btnBrowseScenario.UseVisualStyleBackColor = true;
      this._btnBrowseScenario.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblPathBoot
      // 
      this._lblPathBoot.Location = new System.Drawing.Point(3, 92);
      this._lblPathBoot.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathBoot.Name = "_lblPathBoot";
      this._lblPathBoot.Size = new System.Drawing.Size(175, 20);
      this._lblPathBoot.TabIndex = 25;
      this._lblPathBoot.Text = "Данные загрузки:";
      this._lblPathBoot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbBoot
      // 
      this._tbBoot.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbBoot.Location = new System.Drawing.Point(181, 93);
      this._tbBoot.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbBoot.Name = "_tbBoot";
      this._tbBoot.ReadOnly = true;
      this._tbBoot.Size = new System.Drawing.Size(400, 20);
      this._tbBoot.TabIndex = 26;
      // 
      // _btnBrowseBoot
      // 
      this._btnBrowseBoot.Location = new System.Drawing.Point(590, 92);
      this._btnBrowseBoot.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseBoot.Name = "_btnBrowseBoot";
      this._btnBrowseBoot.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseBoot.TabIndex = 27;
      this._btnBrowseBoot.Tag = this._tbBoot;
      this._btnBrowseBoot.Text = "Обзор...";
      this._btnBrowseBoot.UseVisualStyleBackColor = true;
      this._btnBrowseBoot.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblPathLogs
      // 
      this._lblPathLogs.Location = new System.Drawing.Point(3, 64);
      this._lblPathLogs.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathLogs.Name = "_lblPathLogs";
      this._lblPathLogs.Size = new System.Drawing.Size(175, 20);
      this._lblPathLogs.TabIndex = 22;
      this._lblPathLogs.Text = "Каталог логов:";
      this._lblPathLogs.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbLogs
      // 
      this._tbLogs.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbLogs.Location = new System.Drawing.Point(181, 65);
      this._tbLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbLogs.Name = "_tbLogs";
      this._tbLogs.ReadOnly = true;
      this._tbLogs.Size = new System.Drawing.Size(400, 20);
      this._tbLogs.TabIndex = 23;
      // 
      // _btnBrowseLogs
      // 
      this._btnBrowseLogs.Location = new System.Drawing.Point(590, 64);
      this._btnBrowseLogs.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseLogs.Name = "_btnBrowseLogs";
      this._btnBrowseLogs.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseLogs.TabIndex = 24;
      this._btnBrowseLogs.Tag = this._tbLogs;
      this._btnBrowseLogs.Text = "Обзор...";
      this._btnBrowseLogs.UseVisualStyleBackColor = true;
      this._btnBrowseLogs.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblPathGomeostas
      // 
      this._lblPathGomeostas.Location = new System.Drawing.Point(3, 36);
      this._lblPathGomeostas.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathGomeostas.Name = "_lblPathGomeostas";
      this._lblPathGomeostas.Size = new System.Drawing.Size(175, 20);
      this._lblPathGomeostas.TabIndex = 7;
      this._lblPathGomeostas.Text = "Каталог данных:";
      this._lblPathGomeostas.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbGomeostas
      // 
      this._tbGomeostas.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbGomeostas.Location = new System.Drawing.Point(181, 37);
      this._tbGomeostas.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbGomeostas.Name = "_tbGomeostas";
      this._tbGomeostas.ReadOnly = true;
      this._tbGomeostas.Size = new System.Drawing.Size(400, 20);
      this._tbGomeostas.TabIndex = 8;
      // 
      // _btnBrowseGomeostas
      // 
      this._btnBrowseGomeostas.Location = new System.Drawing.Point(590, 36);
      this._btnBrowseGomeostas.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseGomeostas.Name = "_btnBrowseGomeostas";
      this._btnBrowseGomeostas.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseGomeostas.TabIndex = 9;
      this._btnBrowseGomeostas.Tag = this._tbGomeostas;
      this._btnBrowseGomeostas.Text = "Обзор...";
      this._btnBrowseGomeostas.UseVisualStyleBackColor = true;
      this._btnBrowseGomeostas.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // _lblPathSettings
      // 
      this._lblPathSettings.Location = new System.Drawing.Point(3, 9);
      this._lblPathSettings.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblPathSettings.Name = "_lblPathSettings";
      this._lblPathSettings.Size = new System.Drawing.Size(175, 20);
      this._lblPathSettings.TabIndex = 3;
      this._lblPathSettings.Text = "Каталог настроек:";
      this._lblPathSettings.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbSettingsPath
      // 
      this._tbSettingsPath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(235)))), ((int)(((byte)(235)))));
      this._tbSettingsPath.Location = new System.Drawing.Point(181, 10);
      this._tbSettingsPath.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbSettingsPath.Name = "_tbSettingsPath";
      this._tbSettingsPath.ReadOnly = true;
      this._tbSettingsPath.Size = new System.Drawing.Size(400, 20);
      this._tbSettingsPath.TabIndex = 4;
      // 
      // _btnBrowseSettings
      // 
      this._btnBrowseSettings.Location = new System.Drawing.Point(590, 9);
      this._btnBrowseSettings.Margin = new System.Windows.Forms.Padding(6, 2, 0, 0);
      this._btnBrowseSettings.Name = "_btnBrowseSettings";
      this._btnBrowseSettings.Size = new System.Drawing.Size(75, 23);
      this._btnBrowseSettings.TabIndex = 5;
      this._btnBrowseSettings.Tag = this._tbSettingsPath;
      this._btnBrowseSettings.Text = "Обзор...";
      this._btnBrowseSettings.UseVisualStyleBackColor = true;
      this._btnBrowseSettings.Click += new System.EventHandler(this.PathBrowse_Click);
      // 
      // tabPage2
      // 
      this.tabPage2.Controls.Add(this._chkVerbalAuthoritative);
      this.tabPage2.Controls.Add(this._chkObservationMode);
      this.tabPage2.Controls.Add(this._lblLogFormat);
      this.tabPage2.Controls.Add(this._cmbLogFormat);
      this.tabPage2.Controls.Add(this._lblLogEnabled);
      this.tabPage2.Controls.Add(this._chkLog);
      this.tabPage2.Controls.Add(this._lblSolidHomeostasisDebugLog);
      this.tabPage2.Controls.Add(this._chkSolidHomeostasisDebugLog);
      this.tabPage2.Controls.Add(this._lblSolidHostMinDelta);
      this.tabPage2.Controls.Add(this._tbSolidHostMinDelta);
      this.tabPage2.Controls.Add(this._lblSolidMetricEpsilon);
      this.tabPage2.Controls.Add(this._tbSolidMetricEpsilon);
      this.tabPage2.Controls.Add(this._lblRecognition);
      this.tabPage2.Controls.Add(this._tbRecognition);
      this.tabPage2.Controls.Add(this._lblReflexDur);
      this.tabPage2.Controls.Add(this._tbReflexDur);
      this.tabPage2.Controls.Add(this._lblDynamic);
      this.tabPage2.Controls.Add(this._tbDynamic);
      this.tabPage2.Controls.Add(this._lblDifSensor);
      this.tabPage2.Controls.Add(this._tbDifSensor);
      this.tabPage2.Controls.Add(this._lblHomeostasisPulseDrift);
      this.tabPage2.Controls.Add(this._chkHomeostasisPulseDrift);
      this.tabPage2.Controls.Add(this._lblCompare);
      this.tabPage2.Controls.Add(this._tbCompare);
      this.tabPage2.Controls.Add(this._lblAdaptive);
      this.tabPage2.Controls.Add(this._cmbAdaptive);
      this.tabPage2.Controls.Add(this._lblStyle);
      this.tabPage2.Controls.Add(this._cmbStyle);
      this.tabPage2.Location = new System.Drawing.Point(4, 22);
      this.tabPage2.Name = "tabPage2";
      this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
      this.tabPage2.Size = new System.Drawing.Size(676, 292);
      this.tabPage2.TabIndex = 1;
      this.tabPage2.Text = "Регуляция";
      this.tabPage2.UseVisualStyleBackColor = true;
      // 
      // _chkVerbalAuthoritative
      // 
      this._chkVerbalAuthoritative.AutoSize = true;
      this._chkVerbalAuthoritative.Location = new System.Drawing.Point(374, 7);
      this._chkVerbalAuthoritative.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
      this._chkVerbalAuthoritative.Name = "_chkVerbalAuthoritative";
      this._chkVerbalAuthoritative.Size = new System.Drawing.Size(299, 17);
      this._chkVerbalAuthoritative.TabIndex = 44;
      this._chkVerbalAuthoritative.Text = "Авторитарная запись вербального канала (оператор)";
      this._ttpStageEvolution.SetToolTip(this._chkVerbalAuthoritative, "VerbalAuthoritativeMode и CommandAuthoritativeMode: сразу в дерево (иначе песочни" +
        "ца, RecognitionThreshold).");
      this._chkVerbalAuthoritative.UseVisualStyleBackColor = true;
      // 
      // _chkObservationMode
      // 
      this._chkObservationMode.AutoSize = true;
      this._chkObservationMode.Location = new System.Drawing.Point(374, 32);
      this._chkObservationMode.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
      this._chkObservationMode.Name = "_chkObservationMode";
      this._chkObservationMode.Size = new System.Drawing.Size(275, 17);
      this._chkObservationMode.TabIndex = 45;
      this._chkObservationMode.Text = "Режим наблюдения (без изменения гомеостаза)";
      this._ttpStageEvolution.SetToolTip(this._chkObservationMode, "AppGlobalState.ObservationMode: воздействия с пульта не меняют параметры гомеоста" +
        "за; автоматизмы и рефлексы исполняются.");
      this._chkObservationMode.UseVisualStyleBackColor = true;
      // 
      // _lblLogFormat
      // 
      this._lblLogFormat.Location = new System.Drawing.Point(3, 188);
      this._lblLogFormat.Margin = new System.Windows.Forms.Padding(0, 6, 4, 0);
      this._lblLogFormat.Name = "_lblLogFormat";
      this._lblLogFormat.Size = new System.Drawing.Size(169, 13);
      this._lblLogFormat.TabIndex = 18;
      this._lblLogFormat.Text = "Формат логов:";
      // 
      // _cmbLogFormat
      // 
      this._cmbLogFormat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbLogFormat.Location = new System.Drawing.Point(180, 185);
      this._cmbLogFormat.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
      this._cmbLogFormat.Name = "_cmbLogFormat";
      this._cmbLogFormat.Size = new System.Drawing.Size(150, 21);
      this._cmbLogFormat.TabIndex = 19;
      // 
      // _lblLogEnabled
      // 
      this._lblLogEnabled.Location = new System.Drawing.Point(393, 80);
      this._lblLogEnabled.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblLogEnabled.Name = "_lblLogEnabled";
      this._lblLogEnabled.Size = new System.Drawing.Size(176, 20);
      this._lblLogEnabled.TabIndex = 16;
      this._lblLogEnabled.Text = "Лог событий:";
      this._lblLogEnabled.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _chkLog
      // 
      this._chkLog.AutoSize = true;
      this._chkLog.Location = new System.Drawing.Point(374, 86);
      this._chkLog.Margin = new System.Windows.Forms.Padding(8, 4, 40, 0);
      this._chkLog.Name = "_chkLog";
      this._chkLog.Size = new System.Drawing.Size(15, 14);
       this._chkLog.TabIndex = 17;
       this._chkLog.UseVisualStyleBackColor = true;
       // 
       // _lblSolidHomeostasisDebugLog
       // 
       this._lblSolidHomeostasisDebugLog.Location = new System.Drawing.Point(393, 108);
       this._lblSolidHomeostasisDebugLog.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
       this._lblSolidHomeostasisDebugLog.Name = "_lblSolidHomeostasisDebugLog";
       this._lblSolidHomeostasisDebugLog.Size = new System.Drawing.Size(270, 20);
       this._lblSolidHomeostasisDebugLog.TabIndex = 18;
       this._lblSolidHomeostasisDebugLog.Text = "Отладка SolidHomeostasis (Trace/Debug):";
       this._lblSolidHomeostasisDebugLog.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
       // 
       // _chkSolidHomeostasisDebugLog
       // 
       this._chkSolidHomeostasisDebugLog.AutoSize = true;
       this._chkSolidHomeostasisDebugLog.Location = new System.Drawing.Point(374, 114);
       this._chkSolidHomeostasisDebugLog.Margin = new System.Windows.Forms.Padding(8, 4, 40, 0);
       this._chkSolidHomeostasisDebugLog.Name = "_chkSolidHomeostasisDebugLog";
       this._chkSolidHomeostasisDebugLog.Size = new System.Drawing.Size(15, 14);
       this._chkSolidHomeostasisDebugLog.TabIndex = 18;
       this._chkSolidHomeostasisDebugLog.UseVisualStyleBackColor = true;
       // 
       // _lblSolidHostMinDelta
      // 
      this._lblSolidHostMinDelta.Location = new System.Drawing.Point(3, 162);
      this._lblSolidHostMinDelta.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblSolidHostMinDelta.Name = "_lblSolidHostMinDelta";
      this._lblSolidHostMinDelta.Size = new System.Drawing.Size(169, 13);
      this._lblSolidHostMinDelta.TabIndex = 42;
      this._lblSolidHostMinDelta.Text = "Мин. |ΔP_i| для записи:";
      this._lblSolidHostMinDelta.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      this._ttpStageEvolution.SetToolTip(this._lblSolidHostMinDelta, "Минимальное |целевое P_i − текущее в движке| на шкале 0…100 для записи в ISIDA за" +
        " такт. 0 — фильтр выключен.");
      // 
      // _tbSolidHostMinDelta
      // 
      this._tbSolidHostMinDelta.Location = new System.Drawing.Point(180, 159);
      this._tbSolidHostMinDelta.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbSolidHostMinDelta.Name = "_tbSolidHostMinDelta";
      this._tbSolidHostMinDelta.Size = new System.Drawing.Size(150, 20);
      this._tbSolidHostMinDelta.TabIndex = 43;
      this._ttpStageEvolution.SetToolTip(this._tbSolidHostMinDelta, "Минимальное |целевое P_i − текущее в движке| на шкале 0…100 для записи в ISIDA за" +
        " такт. 0 — фильтр выключен.");
      // 
      // _lblSolidMetricEpsilon
      // 
      this._lblSolidMetricEpsilon.Location = new System.Drawing.Point(3, 139);
      this._lblSolidMetricEpsilon.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblSolidMetricEpsilon.Name = "_lblSolidMetricEpsilon";
      this._lblSolidMetricEpsilon.Size = new System.Drawing.Size(169, 13);
      this._lblSolidMetricEpsilon.TabIndex = 40;
      this._lblSolidMetricEpsilon.Text = "Порог Δ метрики SW:";
      this._lblSolidMetricEpsilon.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      this._ttpStageEvolution.SetToolTip(this._lblSolidMetricEpsilon, "Порог заметного изменения метрики среды SolidWorks между пульсами (шкала 0…100).");
      // 
      // _tbSolidMetricEpsilon
      // 
      this._tbSolidMetricEpsilon.Location = new System.Drawing.Point(180, 132);
      this._tbSolidMetricEpsilon.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbSolidMetricEpsilon.Name = "_tbSolidMetricEpsilon";
      this._tbSolidMetricEpsilon.Size = new System.Drawing.Size(150, 20);
      this._tbSolidMetricEpsilon.TabIndex = 41;
      this._ttpStageEvolution.SetToolTip(this._tbSolidMetricEpsilon, "Порог заметного изменения метрики среды SolidWorks между пульсами (шкала 0…100).");
      // 
      // _lblRecognition
      // 
      this._lblRecognition.Location = new System.Drawing.Point(3, 113);
      this._lblRecognition.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblRecognition.Name = "_lblRecognition";
      this._lblRecognition.Size = new System.Drawing.Size(169, 13);
      this._lblRecognition.TabIndex = 14;
      this._lblRecognition.Text = "Повторы для фиксации образа:";
      this._lblRecognition.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbRecognition
      // 
      this._tbRecognition.Location = new System.Drawing.Point(180, 106);
      this._tbRecognition.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbRecognition.Name = "_tbRecognition";
      this._tbRecognition.Size = new System.Drawing.Size(150, 20);
      this._tbRecognition.TabIndex = 15;
      // 
      // _lblReflexDur
      // 
      this._lblReflexDur.Location = new System.Drawing.Point(3, 87);
      this._lblReflexDur.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblReflexDur.Name = "_lblReflexDur";
      this._lblReflexDur.Size = new System.Drawing.Size(169, 13);
      this._lblReflexDur.TabIndex = 12;
      this._lblReflexDur.Text = "Удержание действия (пульс.):";
      this._lblReflexDur.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbReflexDur
      // 
      this._tbReflexDur.Location = new System.Drawing.Point(180, 81);
      this._tbReflexDur.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbReflexDur.Name = "_tbReflexDur";
      this._tbReflexDur.Size = new System.Drawing.Size(150, 20);
      this._tbReflexDur.TabIndex = 13;
      // 
      // _lblDynamic
      // 
      this._lblDynamic.Location = new System.Drawing.Point(3, 61);
      this._lblDynamic.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDynamic.Name = "_lblDynamic";
      this._lblDynamic.Size = new System.Drawing.Size(169, 13);
      this._lblDynamic.TabIndex = 10;
      this._lblDynamic.Text = "Удержание Плохо/Хорошо (пульс.):";
      this._lblDynamic.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      this._ttpStageEvolution.SetToolTip(this._lblDynamic, resources.GetString("_lblDynamic.ToolTip"));
      // 
      // _tbDynamic
      // 
      this._tbDynamic.Location = new System.Drawing.Point(180, 55);
      this._tbDynamic.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbDynamic.Name = "_tbDynamic";
      this._tbDynamic.Size = new System.Drawing.Size(150, 20);
      this._tbDynamic.TabIndex = 11;
      this._ttpStageEvolution.SetToolTip(this._tbDynamic, resources.GetString("_tbDynamic.ToolTip"));
      // 
      // _lblDifSensor
      // 
      this._lblDifSensor.Location = new System.Drawing.Point(3, 35);
      this._lblDifSensor.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDifSensor.Name = "_lblDifSensor";
      this._lblDifSensor.Size = new System.Drawing.Size(169, 13);
      this._lblDifSensor.TabIndex = 8;
      this._lblDifSensor.Text = "Минимальный шаг параметра:";
      this._lblDifSensor.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbDifSensor
      // 
      this._tbDifSensor.Location = new System.Drawing.Point(180, 32);
      this._tbDifSensor.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbDifSensor.Name = "_tbDifSensor";
      this._tbDifSensor.Size = new System.Drawing.Size(150, 20);
      this._tbDifSensor.TabIndex = 9;
      // 
      // _lblHomeostasisPulseDrift
      // 
      this._lblHomeostasisPulseDrift.Location = new System.Drawing.Point(393, 58);
      this._lblHomeostasisPulseDrift.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblHomeostasisPulseDrift.Name = "_lblHomeostasisPulseDrift";
      this._lblHomeostasisPulseDrift.Size = new System.Drawing.Size(221, 21);
      this._lblHomeostasisPulseDrift.TabIndex = 20;
      this._lblHomeostasisPulseDrift.Text = "Дрейф параметров по Speed на пульсе:";
      // 
      // _chkHomeostasisPulseDrift
      // 
      this._chkHomeostasisPulseDrift.AutoSize = true;
      this._chkHomeostasisPulseDrift.Location = new System.Drawing.Point(374, 61);
      this._chkHomeostasisPulseDrift.Margin = new System.Windows.Forms.Padding(8, 4, 40, 0);
      this._chkHomeostasisPulseDrift.Name = "_chkHomeostasisPulseDrift";
      this._chkHomeostasisPulseDrift.Size = new System.Drawing.Size(15, 14);
      this._chkHomeostasisPulseDrift.TabIndex = 21;
      this._chkHomeostasisPulseDrift.UseVisualStyleBackColor = true;
      // 
      // _lblCompare
      // 
      this._lblCompare.Location = new System.Drawing.Point(3, 7);
      this._lblCompare.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCompare.Name = "_lblCompare";
      this._lblCompare.Size = new System.Drawing.Size(169, 13);
      this._lblCompare.TabIndex = 6;
      this._lblCompare.Text = "Интегральный порог, %:";
      this._lblCompare.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCompare
      // 
      this._tbCompare.Location = new System.Drawing.Point(180, 5);
      this._tbCompare.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbCompare.Name = "_tbCompare";
      this._tbCompare.Size = new System.Drawing.Size(150, 20);
      this._tbCompare.TabIndex = 7;
      // 
      // _lblAdaptive
      // 
      this._lblAdaptive.Location = new System.Drawing.Point(3, 213);
      this._lblAdaptive.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblAdaptive.Name = "_lblAdaptive";
      this._lblAdaptive.Size = new System.Drawing.Size(169, 13);
      this._lblAdaptive.TabIndex = 4;
      this._lblAdaptive.Text = "Адаптивные действия:";
      this._lblAdaptive.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _cmbAdaptive
      // 
      this._cmbAdaptive.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbAdaptive.Location = new System.Drawing.Point(180, 210);
      this._cmbAdaptive.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._cmbAdaptive.Name = "_cmbAdaptive";
      this._cmbAdaptive.Size = new System.Drawing.Size(150, 21);
      this._cmbAdaptive.TabIndex = 5;
      // 
      // _lblStyle
      // 
      this._lblStyle.Location = new System.Drawing.Point(3, 238);
      this._lblStyle.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblStyle.Name = "_lblStyle";
      this._lblStyle.Size = new System.Drawing.Size(169, 13);
      this._lblStyle.TabIndex = 2;
      this._lblStyle.Text = "Стиль поведения агента:";
      this._lblStyle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _cmbStyle
      // 
      this._cmbStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbStyle.Location = new System.Drawing.Point(180, 235);
      this._cmbStyle.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._cmbStyle.Name = "_cmbStyle";
      this._cmbStyle.Size = new System.Drawing.Size(150, 21);
      this._cmbStyle.TabIndex = 3;
      // 
      // tabPage3
      // 
      this.tabPage3.Controls.Add(this._chkFirstRun);
      this.tabPage3.Controls.Add(this._tbDefaultGeneticReflexId);
      this.tabPage3.Controls.Add(this._lblDefaultGeneticReflexId);
      this.tabPage3.Controls.Add(this._lblSilence);
      this.tabPage3.Controls.Add(this._tbSilence);
      this.tabPage3.Controls.Add(this._lblMainMaxAge);
      this.tabPage3.Controls.Add(this._tbMainMaxAge);
      this.tabPage3.Controls.Add(this._lblCycleBase);
      this.tabPage3.Controls.Add(this._tbCycleBase);
      this.tabPage3.Controls.Add(this._lblCycleDiv);
      this.tabPage3.Controls.Add(this._tbCycleDiv);
      this.tabPage3.Controls.Add(this._lblWait);
      this.tabPage3.Controls.Add(this._tbWaitOperator);
      this.tabPage3.Controls.Add(this._lblTheme);
      this.tabPage3.Controls.Add(this._cmbTheme);
      this.tabPage3.Location = new System.Drawing.Point(4, 22);
      this.tabPage3.Name = "tabPage3";
      this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
      this.tabPage3.Size = new System.Drawing.Size(676, 292);
      this.tabPage3.TabIndex = 2;
      this.tabPage3.Text = "Аналитика";
      this.tabPage3.UseVisualStyleBackColor = true;
      // 
      // _chkFirstRun
      // 
      this._chkFirstRun.AutoSize = true;
      this._chkFirstRun.Location = new System.Drawing.Point(8, 192);
      this._chkFirstRun.Margin = new System.Windows.Forms.Padding(8, 4, 40, 0);
      this._chkFirstRun.Name = "_chkFirstRun";
      this._chkFirstRun.Size = new System.Drawing.Size(104, 17);
      this._chkFirstRun.TabIndex = 16;
      this._chkFirstRun.Text = "Первый запуск";
      this._chkFirstRun.UseVisualStyleBackColor = true;
      // 
      // _tbDefaultGeneticReflexId
      // 
      this._tbDefaultGeneticReflexId.Location = new System.Drawing.Point(187, 163);
      this._tbDefaultGeneticReflexId.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbDefaultGeneticReflexId.Name = "_tbDefaultGeneticReflexId";
      this._tbDefaultGeneticReflexId.Size = new System.Drawing.Size(100, 20);
      this._tbDefaultGeneticReflexId.TabIndex = 15;
      // 
      // _lblDefaultGeneticReflexId
      // 
      this._lblDefaultGeneticReflexId.Location = new System.Drawing.Point(5, 162);
      this._lblDefaultGeneticReflexId.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDefaultGeneticReflexId.Name = "_lblDefaultGeneticReflexId";
      this._lblDefaultGeneticReflexId.Size = new System.Drawing.Size(174, 20);
      this._lblDefaultGeneticReflexId.TabIndex = 14;
      this._lblDefaultGeneticReflexId.Text = "Безусловный рефлекс (ID):";
      this._lblDefaultGeneticReflexId.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _lblSilence
      // 
      this._lblSilence.Location = new System.Drawing.Point(5, 136);
      this._lblSilence.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblSilence.Name = "_lblSilence";
      this._lblSilence.Size = new System.Drawing.Size(174, 20);
      this._lblSilence.TabIndex = 12;
      this._lblSilence.Text = "Простой оператора (пульс.):";
      this._lblSilence.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbSilence
      // 
      this._tbSilence.Location = new System.Drawing.Point(187, 137);
      this._tbSilence.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbSilence.Name = "_tbSilence";
      this._tbSilence.Size = new System.Drawing.Size(100, 20);
      this._tbSilence.TabIndex = 13;
      // 
      // _lblMainMaxAge
      // 
      this._lblMainMaxAge.Location = new System.Drawing.Point(5, 110);
      this._lblMainMaxAge.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblMainMaxAge.Name = "_lblMainMaxAge";
      this._lblMainMaxAge.Size = new System.Drawing.Size(174, 20);
      this._lblMainMaxAge.TabIndex = 10;
      this._lblMainMaxAge.Text = "Цикл: max возраст (пульс.):";
      this._lblMainMaxAge.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbMainMaxAge
      // 
      this._tbMainMaxAge.Location = new System.Drawing.Point(187, 111);
      this._tbMainMaxAge.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbMainMaxAge.Name = "_tbMainMaxAge";
      this._tbMainMaxAge.Size = new System.Drawing.Size(100, 20);
      this._tbMainMaxAge.TabIndex = 11;
      // 
      // _lblCycleBase
      // 
      this._lblCycleBase.Location = new System.Drawing.Point(5, 84);
      this._lblCycleBase.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCycleBase.Name = "_lblCycleBase";
      this._lblCycleBase.Size = new System.Drawing.Size(174, 20);
      this._lblCycleBase.TabIndex = 8;
      this._lblCycleBase.Text = "Цикл: снятие B за пульс:";
      this._lblCycleBase.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCycleBase
      // 
      this._tbCycleBase.Location = new System.Drawing.Point(187, 85);
      this._tbCycleBase.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbCycleBase.Name = "_tbCycleBase";
      this._tbCycleBase.Size = new System.Drawing.Size(100, 20);
      this._tbCycleBase.TabIndex = 9;
      // 
      // _lblCycleDiv
      // 
      this._lblCycleDiv.Location = new System.Drawing.Point(5, 58);
      this._lblCycleDiv.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCycleDiv.Name = "_lblCycleDiv";
      this._lblCycleDiv.Size = new System.Drawing.Size(174, 20);
      this._lblCycleDiv.TabIndex = 6;
      this._lblCycleDiv.Text = "Цикл: делитель A (B+age/A):";
      this._lblCycleDiv.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCycleDiv
      // 
      this._tbCycleDiv.Location = new System.Drawing.Point(187, 59);
      this._tbCycleDiv.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbCycleDiv.Name = "_tbCycleDiv";
      this._tbCycleDiv.Size = new System.Drawing.Size(100, 20);
      this._tbCycleDiv.TabIndex = 7;
      // 
      // _lblWait
      // 
      this._lblWait.Location = new System.Drawing.Point(5, 32);
      this._lblWait.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblWait.Name = "_lblWait";
      this._lblWait.Size = new System.Drawing.Size(174, 20);
      this._lblWait.TabIndex = 4;
      this._lblWait.Text = "Ожидание оператора (пульс.):";
      this._lblWait.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbWaitOperator
      // 
      this._tbWaitOperator.Location = new System.Drawing.Point(187, 33);
      this._tbWaitOperator.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._tbWaitOperator.Name = "_tbWaitOperator";
      this._tbWaitOperator.Size = new System.Drawing.Size(100, 20);
      this._tbWaitOperator.TabIndex = 5;
      // 
      // _lblTheme
      // 
      this._lblTheme.Location = new System.Drawing.Point(5, 6);
      this._lblTheme.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblTheme.Name = "_lblTheme";
      this._lblTheme.Size = new System.Drawing.Size(174, 20);
      this._lblTheme.TabIndex = 2;
      this._lblTheme.Text = "Тема анализа:";
      this._lblTheme.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _cmbTheme
      // 
      this._cmbTheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbTheme.Location = new System.Drawing.Point(187, 6);
      this._cmbTheme.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this._cmbTheme.Name = "_cmbTheme";
      this._cmbTheme.Size = new System.Drawing.Size(220, 21);
      this._cmbTheme.TabIndex = 3;
      // 
      // tabPageAdapter
      // 
      this.tabPageAdapter.Controls.Add(this._lblHeavyMetricsPulsePeriod);
      this.tabPageAdapter.Controls.Add(this._tbHeavyMetricsPulsePeriod);
      this.tabPageAdapter.Controls.Add(this._lblCommandBufferFlushHint);
      this.tabPageAdapter.Controls.Add(this._tbCommandBufferMaxAgeSec);
      this.tabPageAdapter.Controls.Add(this._lblCommandBufferMaxAgeSec);
      this.tabPageAdapter.Controls.Add(this._tbCommandBufferMaxTokens);
      this.tabPageAdapter.Controls.Add(this._lblCommandBufferMaxTokens);
      this.tabPageAdapter.Controls.Add(this._tbCommandBufferIdleFlushSec);
      this.tabPageAdapter.Controls.Add(this._lblCommandBufferIdleFlushSec);
      this.tabPageAdapter.Controls.Add(this._lblCommandBufferRecordingHint);
      this.tabPageAdapter.Controls.Add(this._chkCommandBufferRecording);
      this.tabPageAdapter.Location = new System.Drawing.Point(4, 22);
      this.tabPageAdapter.Name = "tabPageAdapter";
      this.tabPageAdapter.Padding = new System.Windows.Forms.Padding(3);
      this.tabPageAdapter.Size = new System.Drawing.Size(676, 292);
      this.tabPageAdapter.TabIndex = 3;
      this.tabPageAdapter.Text = "Адаптер";
      this.tabPageAdapter.UseVisualStyleBackColor = true;
      // 
      // _lblHeavyMetricsPulsePeriod
      // 
      this._lblHeavyMetricsPulsePeriod.Location = new System.Drawing.Point(3, 9);
      this._lblHeavyMetricsPulsePeriod.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblHeavyMetricsPulsePeriod.Name = "_lblHeavyMetricsPulsePeriod";
      this._lblHeavyMetricsPulsePeriod.Size = new System.Drawing.Size(175, 20);
      this._lblHeavyMetricsPulsePeriod.TabIndex = 0;
      this._lblHeavyMetricsPulsePeriod.Text = "Период пульсации метрик:";
      this._lblHeavyMetricsPulsePeriod.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbHeavyMetricsPulsePeriod
      // 
      this._tbHeavyMetricsPulsePeriod.BackColor = System.Drawing.SystemColors.Window;
      this._tbHeavyMetricsPulsePeriod.Location = new System.Drawing.Point(181, 10);
      this._tbHeavyMetricsPulsePeriod.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbHeavyMetricsPulsePeriod.Name = "_tbHeavyMetricsPulsePeriod";
      this._tbHeavyMetricsPulsePeriod.Size = new System.Drawing.Size(150, 20);
      this._tbHeavyMetricsPulsePeriod.TabIndex = 1;
      // 
      // _lblCommandBufferFlushHint
      // 
      this._lblCommandBufferFlushHint.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblCommandBufferFlushHint.Location = new System.Drawing.Point(3, 35);
      this._lblCommandBufferFlushHint.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCommandBufferFlushHint.Name = "_lblCommandBufferFlushHint";
      this._lblCommandBufferFlushHint.Size = new System.Drawing.Size(650, 20);
      this._lblCommandBufferFlushHint.TabIndex = 2;
      this._lblCommandBufferFlushHint.Text = "Параметры буфера команд (для отладки адаптера):";
      this._lblCommandBufferFlushHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCommandBufferMaxAgeSec
      // 
      this._tbCommandBufferMaxAgeSec.BackColor = System.Drawing.SystemColors.Window;
      this._tbCommandBufferMaxAgeSec.Location = new System.Drawing.Point(181, 61);
      this._tbCommandBufferMaxAgeSec.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbCommandBufferMaxAgeSec.Name = "_tbCommandBufferMaxAgeSec";
      this._tbCommandBufferMaxAgeSec.Size = new System.Drawing.Size(150, 20);
      this._tbCommandBufferMaxAgeSec.TabIndex = 3;
      // 
      // _lblCommandBufferMaxAgeSec
      // 
      this._lblCommandBufferMaxAgeSec.Location = new System.Drawing.Point(3, 60);
      this._lblCommandBufferMaxAgeSec.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCommandBufferMaxAgeSec.Name = "_lblCommandBufferMaxAgeSec";
      this._lblCommandBufferMaxAgeSec.Size = new System.Drawing.Size(175, 20);
      this._lblCommandBufferMaxAgeSec.TabIndex = 4;
      this._lblCommandBufferMaxAgeSec.Text = "Макс. возраст буфера (пульсов):";
      this._lblCommandBufferMaxAgeSec.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCommandBufferMaxTokens
      // 
      this._tbCommandBufferMaxTokens.BackColor = System.Drawing.SystemColors.Window;
      this._tbCommandBufferMaxTokens.Location = new System.Drawing.Point(181, 87);
      this._tbCommandBufferMaxTokens.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbCommandBufferMaxTokens.Name = "_tbCommandBufferMaxTokens";
      this._tbCommandBufferMaxTokens.Size = new System.Drawing.Size(150, 20);
      this._tbCommandBufferMaxTokens.TabIndex = 5;
      // 
      // _lblCommandBufferMaxTokens
      // 
      this._lblCommandBufferMaxTokens.Location = new System.Drawing.Point(3, 86);
      this._lblCommandBufferMaxTokens.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCommandBufferMaxTokens.Name = "_lblCommandBufferMaxTokens";
      this._lblCommandBufferMaxTokens.Size = new System.Drawing.Size(175, 20);
      this._lblCommandBufferMaxTokens.TabIndex = 6;
      this._lblCommandBufferMaxTokens.Text = "Макс. токенов в буфере:";
      this._lblCommandBufferMaxTokens.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbCommandBufferIdleFlushSec
      // 
      this._tbCommandBufferIdleFlushSec.BackColor = System.Drawing.SystemColors.Window;
      this._tbCommandBufferIdleFlushSec.Location = new System.Drawing.Point(181, 113);
      this._tbCommandBufferIdleFlushSec.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbCommandBufferIdleFlushSec.Name = "_tbCommandBufferIdleFlushSec";
      this._tbCommandBufferIdleFlushSec.Size = new System.Drawing.Size(150, 20);
      this._tbCommandBufferIdleFlushSec.TabIndex = 7;
      // 
      // _lblCommandBufferIdleFlushSec
      // 
      this._lblCommandBufferIdleFlushSec.Location = new System.Drawing.Point(3, 112);
      this._lblCommandBufferIdleFlushSec.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCommandBufferIdleFlushSec.Name = "_lblCommandBufferIdleFlushSec";
      this._lblCommandBufferIdleFlushSec.Size = new System.Drawing.Size(175, 20);
      this._lblCommandBufferIdleFlushSec.TabIndex = 8;
      this._lblCommandBufferIdleFlushSec.Text = "Сброс простоя (пульсов):";
      this._lblCommandBufferIdleFlushSec.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _lblCommandBufferRecordingHint
      // 
      this._lblCommandBufferRecordingHint.Location = new System.Drawing.Point(3, 144);
      this._lblCommandBufferRecordingHint.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblCommandBufferRecordingHint.Name = "_lblCommandBufferRecordingHint";
      this._lblCommandBufferRecordingHint.Size = new System.Drawing.Size(175, 20);
      this._lblCommandBufferRecordingHint.TabIndex = 9;
      this._lblCommandBufferRecordingHint.Text = "Запись команд в буфер:";
      this._lblCommandBufferRecordingHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _chkCommandBufferRecording
      // 
      this._chkCommandBufferRecording.AutoSize = true;
      this._chkCommandBufferRecording.Checked = true;
      this._chkCommandBufferRecording.CheckState = System.Windows.Forms.CheckState.Checked;
      this._chkCommandBufferRecording.Location = new System.Drawing.Point(180, 147);
      this._chkCommandBufferRecording.Margin = new System.Windows.Forms.Padding(8, 4, 40, 0);
      this._chkCommandBufferRecording.Name = "_chkCommandBufferRecording";
      this._chkCommandBufferRecording.Size = new System.Drawing.Size(15, 14);
      this._chkCommandBufferRecording.TabIndex = 10;
      this._chkCommandBufferRecording.UseVisualStyleBackColor = true;
      // 
      // tabPageDocuments
      // 
      this.tabPageDocuments.Controls.Add(this._chkNeedDrawingDefault);
      this.tabPageDocuments.Controls.Add(this._chkNeedPdfDefault);
      this.tabPageDocuments.Controls.Add(this._chkNeedDxfDefault);
      this.tabPageDocuments.Controls.Add(this._lblDocumentColorsHint);
      this.tabPageDocuments.Controls.Add(this._lblDocumentColorPart);
      this.tabPageDocuments.Controls.Add(this._tbDocumentColorPart);
      this.tabPageDocuments.Controls.Add(this._lblDocumentColorAssembly);
      this.tabPageDocuments.Controls.Add(this._tbDocumentColorAssembly);
      this.tabPageDocuments.Controls.Add(this._lblDocumentColorDrawing);
      this.tabPageDocuments.Controls.Add(this._tbDocumentColorDrawing);
      this.tabPageDocuments.Location = new System.Drawing.Point(4, 22);
      this.tabPageDocuments.Name = "tabPageDocuments";
      this.tabPageDocuments.Padding = new System.Windows.Forms.Padding(3);
      this.tabPageDocuments.Size = new System.Drawing.Size(676, 292);
      this.tabPageDocuments.TabIndex = 4;
      this.tabPageDocuments.Text = "Документы";
      this.tabPageDocuments.UseVisualStyleBackColor = true;
      // 
      // _chkNeedDrawingDefault
      // 
      this._chkNeedDrawingDefault.AutoSize = true;
      this._chkNeedDrawingDefault.Location = new System.Drawing.Point(8, 58);
      this._chkNeedDrawingDefault.Name = "_chkNeedDrawingDefault";
      this._chkNeedDrawingDefault.Size = new System.Drawing.Size(411, 17);
      this._chkNeedDrawingDefault.TabIndex = 2;
      this._chkNeedDrawingDefault.Text = "По умолчанию «Нужен чертеж = Да» при создании новых деталей и сборок";
      this._chkNeedDrawingDefault.UseVisualStyleBackColor = true;
      // 
      // _chkNeedPdfDefault
      // 
      this._chkNeedPdfDefault.AutoSize = true;
      this._chkNeedPdfDefault.Location = new System.Drawing.Point(8, 35);
      this._chkNeedPdfDefault.Name = "_chkNeedPdfDefault";
      this._chkNeedPdfDefault.Size = new System.Drawing.Size(349, 17);
      this._chkNeedPdfDefault.TabIndex = 1;
      this._chkNeedPdfDefault.Text = "По умолчанию «Нужен pdf = Да» при создании новых чертежей";
      this._chkNeedPdfDefault.UseVisualStyleBackColor = true;
      // 
      // _chkNeedDxfDefault
      // 
      this._chkNeedDxfDefault.AutoSize = true;
      this._chkNeedDxfDefault.Location = new System.Drawing.Point(8, 12);
      this._chkNeedDxfDefault.Name = "_chkNeedDxfDefault";
      this._chkNeedDxfDefault.Size = new System.Drawing.Size(341, 17);
      this._chkNeedDxfDefault.TabIndex = 0;
      this._chkNeedDxfDefault.Text = "По умолчанию «Нужен dxf = Да» при создании новых деталей";
      this._chkNeedDxfDefault.UseVisualStyleBackColor = true;
      // 
      // _lblDocumentColorsHint
      // 
      this._lblDocumentColorsHint.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblDocumentColorsHint.Location = new System.Drawing.Point(3, 92);
      this._lblDocumentColorsHint.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDocumentColorsHint.Name = "_lblDocumentColorsHint";
      this._lblDocumentColorsHint.Size = new System.Drawing.Size(650, 20);
      this._lblDocumentColorsHint.TabIndex = 3;
      this._lblDocumentColorsHint.Text = "Коды зрительного канала по типу активного документа (для контекста у-рефлексов):";
      this._lblDocumentColorsHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _lblDocumentColorPart
      // 
      this._lblDocumentColorPart.Location = new System.Drawing.Point(3, 118);
      this._lblDocumentColorPart.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDocumentColorPart.Name = "_lblDocumentColorPart";
      this._lblDocumentColorPart.Size = new System.Drawing.Size(175, 20);
      this._lblDocumentColorPart.TabIndex = 4;
      this._lblDocumentColorPart.Text = "Деталь (код цвета):";
      this._lblDocumentColorPart.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbDocumentColorPart
      // 
      this._tbDocumentColorPart.BackColor = System.Drawing.SystemColors.Window;
      this._tbDocumentColorPart.Location = new System.Drawing.Point(181, 119);
      this._tbDocumentColorPart.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbDocumentColorPart.Name = "_tbDocumentColorPart";
      this._tbDocumentColorPart.Size = new System.Drawing.Size(150, 20);
      this._tbDocumentColorPart.TabIndex = 5;
      // 
      // _lblDocumentColorAssembly
      // 
      this._lblDocumentColorAssembly.Location = new System.Drawing.Point(3, 144);
      this._lblDocumentColorAssembly.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDocumentColorAssembly.Name = "_lblDocumentColorAssembly";
      this._lblDocumentColorAssembly.Size = new System.Drawing.Size(175, 20);
      this._lblDocumentColorAssembly.TabIndex = 6;
      this._lblDocumentColorAssembly.Text = "Сборка (код цвета):";
      this._lblDocumentColorAssembly.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbDocumentColorAssembly
      // 
      this._tbDocumentColorAssembly.BackColor = System.Drawing.SystemColors.Window;
      this._tbDocumentColorAssembly.Location = new System.Drawing.Point(181, 145);
      this._tbDocumentColorAssembly.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbDocumentColorAssembly.Name = "_tbDocumentColorAssembly";
      this._tbDocumentColorAssembly.Size = new System.Drawing.Size(150, 20);
      this._tbDocumentColorAssembly.TabIndex = 7;
      // 
      // _lblDocumentColorDrawing
      // 
      this._lblDocumentColorDrawing.Location = new System.Drawing.Point(3, 170);
      this._lblDocumentColorDrawing.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblDocumentColorDrawing.Name = "_lblDocumentColorDrawing";
      this._lblDocumentColorDrawing.Size = new System.Drawing.Size(175, 20);
      this._lblDocumentColorDrawing.TabIndex = 8;
      this._lblDocumentColorDrawing.Text = "Чертёж (код цвета):";
      this._lblDocumentColorDrawing.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbDocumentColorDrawing
      // 
      this._tbDocumentColorDrawing.BackColor = System.Drawing.SystemColors.Window;
      this._tbDocumentColorDrawing.Location = new System.Drawing.Point(181, 171);
      this._tbDocumentColorDrawing.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbDocumentColorDrawing.Name = "_tbDocumentColorDrawing";
      this._tbDocumentColorDrawing.Size = new System.Drawing.Size(150, 20);
      this._tbDocumentColorDrawing.TabIndex = 9;
      // 
      // _lblStage2SearchPlayStyleIds
      // 
      this._lblStage2SearchPlayStyleIds.Location = new System.Drawing.Point(3, 260);
      this._lblStage2SearchPlayStyleIds.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblStage2SearchPlayStyleIds.Name = "_lblStage2SearchPlayStyleIds";
      this._lblStage2SearchPlayStyleIds.Size = new System.Drawing.Size(169, 13);
      this._lblStage2SearchPlayStyleIds.TabIndex = 4;
      this._lblStage2SearchPlayStyleIds.Text = "Коды стилей Поиск/Игра (ст. 2):";
      this._lblStage2SearchPlayStyleIds.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _tbStage2SearchPlayStyleIds
      // 
      this._tbStage2SearchPlayStyleIds.Location = new System.Drawing.Point(180, 257);
      this._tbStage2SearchPlayStyleIds.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
      this._tbStage2SearchPlayStyleIds.Name = "_tbStage2SearchPlayStyleIds";
      this._tbStage2SearchPlayStyleIds.Size = new System.Drawing.Size(150, 20);
      this._tbStage2SearchPlayStyleIds.TabIndex = 5;
      this._tbStage2SearchPlayStyleIds.TextChanged += new System.EventHandler(this.Stage2StyleIds_TextChanged);
      // 
      // _btnCancel
      // 
      this._btnCancel.AutoSize = true;
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Location = new System.Drawing.Point(618, 337);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(75, 25);
      this._btnCancel.TabIndex = 3;
      this._btnCancel.Text = "Отмена";
      this._btnCancel.UseVisualStyleBackColor = true;
      // 
      // _btnSave
      // 
      this._btnSave.AutoSize = true;
      this._btnSave.Location = new System.Drawing.Point(473, 337);
      this._btnSave.Name = "_btnSave";
      this._btnSave.Size = new System.Drawing.Size(137, 25);
      this._btnSave.TabIndex = 2;
      this._btnSave.Text = "Сохранить настройки";
      this._btnSave.UseVisualStyleBackColor = true;
      this._btnSave.Click += new System.EventHandler(this.OnSaveClick);
      // 
      // _lblStageEvolution
      // 
      this._lblStageEvolution.Location = new System.Drawing.Point(13, 335);
      this._lblStageEvolution.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._lblStageEvolution.Name = "_lblStageEvolution";
      this._lblStageEvolution.Size = new System.Drawing.Size(154, 20);
      this._lblStageEvolution.TabIndex = 4;
      this._lblStageEvolution.Text = "Стадия развития агента:";
      this._lblStageEvolution.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // stage_evolution
      // 
      this.stage_evolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.stage_evolution.Location = new System.Drawing.Point(203, 336);
      this.stage_evolution.Margin = new System.Windows.Forms.Padding(8, 2, 0, 0);
      this.stage_evolution.Name = "stage_evolution";
      this.stage_evolution.Size = new System.Drawing.Size(150, 21);
      this.stage_evolution.TabIndex = 5;
      // 
      // _pnlStageBusyOverlay
      // 
      this._pnlStageBusyOverlay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
      this._pnlStageBusyOverlay.Controls.Add(this._lblStageBusy);
      this._pnlStageBusyOverlay.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pnlStageBusyOverlay.Location = new System.Drawing.Point(0, 0);
      this._pnlStageBusyOverlay.Name = "_pnlStageBusyOverlay";
      this._pnlStageBusyOverlay.Size = new System.Drawing.Size(704, 368);
      this._pnlStageBusyOverlay.TabIndex = 6;
      this._pnlStageBusyOverlay.Visible = false;
      // 
      // _lblStageBusy
      // 
      this._lblStageBusy.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblStageBusy.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._lblStageBusy.Location = new System.Drawing.Point(0, 0);
      this._lblStageBusy.Name = "_lblStageBusy";
      this._lblStageBusy.Size = new System.Drawing.Size(704, 368);
      this._lblStageBusy.TabIndex = 0;
      this._lblStageBusy.Text = "Сброс данных при смене стадии…";
      this._lblStageBusy.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // VelumProjectSettingsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(704, 368);
      this.Controls.Add(this.tabControl1);
      this.Controls.Add(this._lblStageEvolution);
      this.Controls.Add(this.stage_evolution);
      this.Controls.Add(this._btnSave);
      this.Controls.Add(this._btnCancel);
      this.Controls.Add(this._pnlStageBusyOverlay);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "VelumProjectSettingsForm";
      this.Text = "Настройки проекта";
      this.tabControl1.ResumeLayout(false);
      this.tabPage1.ResumeLayout(false);
      this.tabPage1.PerformLayout();
      this.tabPage2.ResumeLayout(false);
      this.tabPage2.PerformLayout();
      this.tabPage3.ResumeLayout(false);
      this.tabPage3.PerformLayout();
      this.tabPageAdapter.ResumeLayout(false);
      this.tabPageAdapter.PerformLayout();
      this.tabPageDocuments.ResumeLayout(false);
      this.tabPageDocuments.PerformLayout();
      this._pnlStageBusyOverlay.ResumeLayout(false);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TabControl tabControl1;
    private System.Windows.Forms.TabPage tabPage1;
    private System.Windows.Forms.Label _lblPathSettings;
    private System.Windows.Forms.TextBox _tbSettingsPath;
    private System.Windows.Forms.Button _btnBrowseSettings;
    private System.Windows.Forms.Label _lblPathGomeostas;
    private System.Windows.Forms.TextBox _tbGomeostas;
    private System.Windows.Forms.Button _btnBrowseGomeostas;
    private System.Windows.Forms.Label _lblPathLogs;
    private System.Windows.Forms.TextBox _tbLogs;
    private System.Windows.Forms.Button _btnBrowseLogs;
    private System.Windows.Forms.Button _btnCancel;
    private System.Windows.Forms.Button _btnSave;
    private System.Windows.Forms.Label _lblPathBoot;
    private System.Windows.Forms.TextBox _tbBoot;
    private System.Windows.Forms.Button _btnBrowseBoot;
    private System.Windows.Forms.Label _lblPathScenario;
    private System.Windows.Forms.TextBox _tbScenarioReports;
    private System.Windows.Forms.Button _btnBrowseScenario;
    private System.Windows.Forms.Label _lblPathProductRegistry;
    private System.Windows.Forms.TextBox _tbProductRegistry;
    private System.Windows.Forms.Button _btnBrowseProductRegistry;
    private System.Windows.Forms.TabPage tabPage2;
    private System.Windows.Forms.Label _lblLogFormat;
    private System.Windows.Forms.ComboBox _cmbLogFormat;
    private System.Windows.Forms.Label _lblSolidMetricEpsilon;
    private System.Windows.Forms.TextBox _tbSolidMetricEpsilon;
    private System.Windows.Forms.Label _lblSolidHostMinDelta;
    private System.Windows.Forms.TextBox _tbSolidHostMinDelta;
    private System.Windows.Forms.Label _lblLogEnabled;
    private System.Windows.Forms.CheckBox _chkLog;
    private System.Windows.Forms.Label _lblSolidHomeostasisDebugLog;
    private System.Windows.Forms.CheckBox _chkSolidHomeostasisDebugLog;
    private System.Windows.Forms.CheckBox _chkVerbalAuthoritative;
    private System.Windows.Forms.CheckBox _chkObservationMode;
    private System.Windows.Forms.Label _lblRecognition;
    private System.Windows.Forms.TextBox _tbRecognition;
    private System.Windows.Forms.Label _lblReflexDur;
    private System.Windows.Forms.TextBox _tbReflexDur;
    private System.Windows.Forms.Label _lblDynamic;
    private System.Windows.Forms.TextBox _tbDynamic;
    private System.Windows.Forms.Label _lblDifSensor;
    private System.Windows.Forms.TextBox _tbDifSensor;
    private System.Windows.Forms.Label _lblCompare;
    private System.Windows.Forms.TextBox _tbCompare;
    private System.Windows.Forms.Label _lblHomeostasisPulseDrift;
    private System.Windows.Forms.CheckBox _chkHomeostasisPulseDrift;
    private System.Windows.Forms.Label _lblAdaptive;
    private System.Windows.Forms.ComboBox _cmbAdaptive;
    private System.Windows.Forms.Label _lblStyle;
    private System.Windows.Forms.ComboBox _cmbStyle;
    private System.Windows.Forms.TabPage tabPage3;
    private System.Windows.Forms.Label _lblTheme;
    private System.Windows.Forms.ComboBox _cmbTheme;
    private System.Windows.Forms.Label _lblWait;
    private System.Windows.Forms.TextBox _tbWaitOperator;
    private System.Windows.Forms.Label _lblCycleDiv;
    private System.Windows.Forms.TextBox _tbCycleDiv;
    private System.Windows.Forms.Label _lblCycleBase;
    private System.Windows.Forms.TextBox _tbCycleBase;
    private System.Windows.Forms.Label _lblMainMaxAge;
    private System.Windows.Forms.TextBox _tbMainMaxAge;
    private System.Windows.Forms.Label _lblSilence;
    private System.Windows.Forms.TextBox _tbSilence;
    private System.Windows.Forms.Label _lblDefaultGeneticReflexId;
    private System.Windows.Forms.TextBox _tbDefaultGeneticReflexId;
    private System.Windows.Forms.CheckBox _chkFirstRun;
    private System.Windows.Forms.Label _lblStageEvolution;
    private System.Windows.Forms.ComboBox stage_evolution;
    private System.Windows.Forms.ToolTip _ttpStageEvolution;
    private System.Windows.Forms.Panel _pnlStageBusyOverlay;
    private System.Windows.Forms.Label _lblStageBusy;

    private System.Windows.Forms.TabPage tabPageDocuments;
    private System.Windows.Forms.TabPage tabPageAdapter;
    private System.Windows.Forms.Label _lblHeavyMetricsPulsePeriod;
    private System.Windows.Forms.TextBox _tbHeavyMetricsPulsePeriod;
    private System.Windows.Forms.Label _lblCommandBufferFlushHint;
    private System.Windows.Forms.TextBox _tbCommandBufferMaxAgeSec;
    private System.Windows.Forms.Label _lblCommandBufferMaxAgeSec;
    private System.Windows.Forms.TextBox _tbCommandBufferMaxTokens;
    private System.Windows.Forms.Label _lblCommandBufferMaxTokens;
    private System.Windows.Forms.TextBox _tbCommandBufferIdleFlushSec;
    private System.Windows.Forms.Label _lblCommandBufferIdleFlushSec;
    private System.Windows.Forms.Label _lblCommandBufferRecordingHint;
    private System.Windows.Forms.CheckBox _chkCommandBufferRecording;
  }
}