using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.Isida;
using Velum.Properties;
using Velum.SolidHomeostasis;
using Velum.UI;
using Xarial.XCad;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.Extensions;
using Xarial.XCad.Extensions.Attributes;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.UI;
using Xarial.XCad.UI.Commands;
using Xarial.XCad.UI.Commands.Attributes;
using Xarial.XCad.UI.Commands.Enums;
using Xarial.XCad.UI.Commands.Structures;
using Xarial.XCad.UI.TaskPane;
using ISIDA.Gomeostas;

namespace Velum
{
  /// <summary>
  /// Точка входа надстройки SolidWorks (XCad <c>SwAddInEx</c>): команды тулбара и ленты, вкладка «Агент» на панели задач, хост ISIDA, пульсация.
  /// </summary>
  [ComVisible(true)]
  [Guid("E9F8D7C6-B5A4-4321-9F0E-8D7C6B5A4931")]
  [SkipRegistration]
  public class VelumAddIn : SwAddInEx
  {
    private static VelumPulseController _pulse;

    private IEnumCommandGroup<Velum_Commands> _velumCommands;

    private VelumProjectSettingsForm _openProjectSettingsForm;

    private IXCustomPanel<VelumAgentTaskPane> _agentTaskPane;

    /// <inheritdoc />
    public override void OnConnect()
    {
      _agentTaskPane = CreateTaskPane<VelumAgentTaskPane>(new TaskPaneSpec());

      RegisterVelumSolidSession();

      // Инициализируем ISIDA сразу после регистрации сессии SW,
      // чтобы события SolidWorks начали обрабатываться независимо от пульсации
      if (!VelumIsidaHost.IsReady)
      {
        if (VelumIsidaHost.TryInitialize(out string initErr))
          ISIDA.Common.Logger.Info("Velum ISIDA initialized on add-in connect");
        else
          ISIDA.Common.Logger.Warning("Velum ISIDA init failed on connect: " + initErr);
      }

      _velumCommands = CommandManager.AddCommandGroup<Velum_Commands>();
      _velumCommands.CommandClick += OnCommandClick;
      _velumCommands.CommandStateResolve += OnVelumCommandStateResolve;

      RegisterGlobalTimerVelumHandlers();
    }

    /// <inheritdoc />
    public override void OnDisconnect()
    {
      GlobalTimer.PulsationStateChanged -= OnGlobalPulsationStateChanged;

      if (_agentTaskPane != null)
      {
        _agentTaskPane.ControlCreated -= OnAgentTaskPaneControlCreated;
      }

      IDisposable disposableTaskPane = _agentTaskPane as IDisposable;
      if (disposableTaskPane != null)
      {
        disposableTaskPane.Dispose();
      }

      _agentTaskPane = null;

      if (_velumCommands != null)
      {
        _velumCommands.CommandClick -= OnCommandClick;
        _velumCommands.CommandStateResolve -= OnVelumCommandStateResolve;
        IDisposable disposableCommands = _velumCommands as IDisposable;
        if (disposableCommands != null)
        {
          disposableCommands.Dispose();
        }

        _velumCommands = null;
      }

      try
      {
        if (_pulse != null)
        {
          _pulse.Dispose();
          _pulse = null;
        }
      }
      finally
      {
        VelumSolidEnvironmentBridge.ClearSolidWorksSession();
        VelumIsidaHost.Shutdown();
      }
    }

    private void RegisterVelumSolidSession()
    {
      if (_agentTaskPane == null)
        return;

      if (_agentTaskPane.IsControlCreated)
      {
        VelumSolidEnvironmentBridge.SetSolidWorksSession(Application, _agentTaskPane.Control);
        WireAgentTaskPaneCommands(_agentTaskPane.Control as VelumAgentTaskPane);
      }
      else
      {
        VelumSolidEnvironmentBridge.SetSolidWorksSession(Application, null);
        _agentTaskPane.ControlCreated += OnAgentTaskPaneControlCreated;
      }

      VelumSolidEnvironmentBridge.SyncSolidPollingWithPulseState();
    }

    private void OnAgentTaskPaneControlCreated(VelumAgentTaskPane ctrl)
    {
      _agentTaskPane.ControlCreated -= OnAgentTaskPaneControlCreated;
      VelumSolidEnvironmentBridge.SetSolidWorksSession(Application, ctrl);
      VelumSolidEnvironmentBridge.SyncSolidPollingWithPulseState();
      WireAgentTaskPaneCommands(ctrl);
    }

    private void WireAgentTaskPaneCommands(VelumAgentTaskPane pane)
    {
      if (pane == null)
        return;
      pane.VelumPulseStartRequested = StartPulse;
      pane.VelumPulseStopRequested = StopPulse;
      pane.VelumProjectSettingsRequested = OpenProjectSettingsDialog;
      pane.RefreshVelumCommandButtons();
    }

    /// <summary>
    /// Полная перезагрузка ISIDA из <see cref="VelumAppConfig"/> после сохранения настроек (аналог AIStudio).
    /// Пульсация должна быть остановлена (форма настроек открывается только при остановленной пульсации).
    /// </summary>
    private void ReloadIsidaRuntimeWithFreshSettings()
    {
      if (GlobalTimer.IsPulsationRunning)
        VelumAdapterSleepReset.PrepareEngineSleepWhilePulseRunning();
      GlobalTimer.Stop();

      VelumIsidaHost.Shutdown();
      if (!VelumIsidaHost.TryInitialize(out string err))
        throw new InvalidOperationException(err ?? "Инициализация ISIDA не удалась.");

      // GlobalTimer.ClearSystems() при Dispose контекста обнулил PulsationStateChanged и др. — восстанавливаем Velum.
      RegisterGlobalTimerVelumHandlers();
      if (_agentTaskPane != null && _agentTaskPane.IsControlCreated)
      {
        var pane = _agentTaskPane.Control as VelumAgentTaskPane;
        pane?.RehookGlobalTimerAfterIsidaReload();
      }

      VelumSolidEnvironmentBridge.SyncSolidPollingWithPulseState();
    }

    private void RegisterGlobalTimerVelumHandlers()
    {
      GlobalTimer.PulsationStateChanged -= OnGlobalPulsationStateChanged;
      GlobalTimer.PulsationStateChanged += OnGlobalPulsationStateChanged;
    }

    private void OpenProjectSettingsDialog()
    {
      try
      {
        if (!VelumProjectSettingsForm.TryAllowOpenProjectSettings(null))
          return;

        using (var dlg = new VelumProjectSettingsForm())
        {
          _openProjectSettingsForm = dlg;
          dlg.StageTransitionHandler = OnEvolutionStageTransitionRequested;
          dlg.ReloadRuntimeAfterSettingsSave = ReloadIsidaRuntimeWithFreshSettings;
          dlg.RefreshEvolutionStageUiFromEngine();
          dlg.ShowDialog();
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        _openProjectSettingsForm = null;
      }
    }

    private void OnGlobalPulsationStateChanged()
    {
      // SyncSolidPollingWithPulseState вызывается из VelumEnginePulseBridge.OnPulsationStateChanged.
      RefreshOpenSettingsEvolutionUi();

      if (_agentTaskPane != null && _agentTaskPane.IsControlCreated)
        (_agentTaskPane.Control as VelumAgentTaskPane)?.RefreshVelumCommandButtons();
    }

    private void RefreshOpenSettingsEvolutionUi()
    {
      try
      {
        _openProjectSettingsForm?.RefreshEvolutionStageUiFromEngine();
      }
      catch
      {
      }
    }

    private bool OnEvolutionStageTransitionRequested(int newStage)
    {
      VelumProjectSettingsForm settingsForm = _openProjectSettingsForm;

      if (!VelumIsidaHost.TryInitialize(out string initErr))
      {
        MessageBox.Show(
            "Не удалось инициализировать ISIDA:\n" + initErr,
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      if (GlobalTimer.IsPulsationRunning)
      {
        MessageBox.Show(
            "Смена стадии недоступна во время пульсации.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      var gomeostas = VelumIsidaHost.Context.Gomeostas;
      var state = gomeostas.GetAgentState();
      if (state == null)
        return false;

      int current = state.EvolutionStage;
      if (newStage == current)
        return true;

      if (newStage > current + 1)
      {
        MessageBox.Show(
            "Недопустимый переход! Можно переходить только на следующую стадию.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      bool backward = newStage < current;
      string title = backward ? "ВНИМАНИЕ: Возврат на предыдущую стадию" : "Подтверждение перехода";
      string message = backward
          ? "Возврат на стадию " + newStage + " очистит все данные последующих стадий. Продолжить?"
          : "Перейти со стадии " + current + " на стадию " + newStage + "?";

      if (MessageBox.Show(
              message,
              title,
              MessageBoxButtons.YesNo,
              backward ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes)
      {
        return false;
      }

      EvolutionStageChangeResult stageResult;

      if (backward)
      {
        try
        {
          settingsForm?.SetEvolutionStageBusyOverlayVisible(true);
          System.Threading.Tasks.Task<EvolutionStageChangeResult> job =
              System.Threading.Tasks.Task.Run(() => gomeostas.SetEvolutionStage(newStage, true, false));
          stageResult = job.GetAwaiter().GetResult();
        }
        finally
        {
          settingsForm?.SetEvolutionStageBusyOverlayVisible(false);
        }
      }
      else
      {
        stageResult = gomeostas.SetEvolutionStage(newStage, false, false);
        if (stageResult.RequiresConfirmation)
        {
          if (MessageBox.Show(
                  stageResult.Message,
                  "Подтверждение",
                  MessageBoxButtons.YesNo,
                  MessageBoxIcon.Warning) != DialogResult.Yes)
          {
            return false;
          }

          stageResult = gomeostas.SetEvolutionStage(newStage, true, false);
        }
      }

      if (!stageResult.Success)
      {
        MessageBox.Show(stageResult.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
      }

      var (saveSuccess, saveErr) = gomeostas.SaveAgentProperties();
      if (!saveSuccess)
      {
        string expectedPropsPath = Path.Combine(
            VelumAppConfig.DataFolderPath ?? string.Empty,
            "AgentProperties.dat");
        MessageBox.Show(
            BuildAgentPropertiesSaveFailureMessage(saveErr, expectedPropsPath),
            "Предупреждение",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
      else
      {
        MessageBox.Show(
            stageResult.Message,
            "Изменение стадии развития агента",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }

      RefreshOpenSettingsEvolutionUi();
      return true;
    }

    private static string FormatExceptionForUi(Exception ex)
    {
      if (ex == null)
        return string.Empty;

      var sb = new StringBuilder(ex.Message);
      for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
      {
        sb.AppendLine();
        sb.AppendLine(inner.Message);
      }

      return sb.ToString();
    }

    private static string BuildAgentPropertiesSaveFailureMessage(string saveErr, string expectedPropsPath)
    {
      var sb = new StringBuilder();
      sb.AppendLine("Ошибка сохранения свойств агента:");
      sb.AppendLine(saveErr ?? string.Empty);
      sb.AppendLine();
      if (!string.IsNullOrEmpty(saveErr) &&
          saveErr.IndexOf("администратора", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        sb.AppendLine(
            "Для записи в ProgramData запуск SolidWorks от администратора обычно не требуется.");
        sb.AppendLine(
            "Чаще мешают права на папку (Свойства → Безопасность для C:\\ProgramData\\VELUM), атрибут «только чтение» у AgentProperties.dat / .tmp / .bak или открытие этих файлов в другой программе.");
        sb.AppendLine();
      }

      sb.AppendLine("Ожидаемый файл:");
      sb.AppendLine(expectedPropsPath);
      sb.AppendLine();
      sb.AppendLine("Проверьте путь DataFolderPath в %ProgramData%\\VELUM\\Settings\\Settings.xml.");
      sb.AppendLine();
      sb.AppendLine(TryProbeGomeostasWritable());
      return sb.ToString();
    }

    private static string TryProbeGomeostasWritable()
    {
      try
      {
        string dir = IsidaDataPaths.ResolveGomeostasFolder(VelumAppConfig.DataFolderPath);
        if (string.IsNullOrWhiteSpace(dir))
          return "Диагностика: в настройках пуст путь к каталогу гомеостаза.";

        Directory.CreateDirectory(dir);
        string probe = Path.Combine(dir, ".velum_write_probe.tmp");
        File.WriteAllText(probe, "ok", Encoding.UTF8);
        File.Delete(probe);
        return
            "Диагностика: тестовая запись в каталог гомеостаза прошла успешно. Тогда сбой, скорее всего, при замене существующих AgentProperties.dat / .tmp / .bak (снимите «только чтение», закройте внешние программы, открывшие эти файлы).";
      }
      catch (Exception ex)
      {
        return "Диагностика: тестовая запись в каталог гомеостаза не удалась: " + ex.Message;
      }
    }

    private void OnCommandClick(Velum_Commands cmd)
    {
      switch (cmd)
      {
        case Velum_Commands.AgentStart:
          StartPulse();
          break;

        case Velum_Commands.AgentStop:
          StopPulse();
          break;

        case Velum_Commands.ProjectSettings:
          OpenProjectSettingsDialog();
          break;

        case Velum_Commands.DxfBatchDiagnostics:
          OpenDxfBatchDiagnosticsForm();
          break;

        case Velum_Commands.PdfBatchDiagnostics:
          OpenPdfBatchDiagnosticsForm();
          break;

        case Velum_Commands.MaterialBatch:
          OpenMaterialBatchForm();
          break;

        case Velum_Commands.DocumentPropertyBatch:
          OpenDocumentPropertyBatchForm();
          break;

        case Velum_Commands.ProductRegistry:
          OpenProductRegistryForm();
          break;

        case Velum_Commands.AssemblyRegistry:
          OpenAssemblyRegistryForm();
          break;

        case Velum_Commands.TechRequirements:
          OpenTechRequirementsForm();
          break;

        case Velum_Commands.Help:
          OpenHelp();
          break;
      }
    }

    private void OpenHelp()
    {
      try
      {
        VelumHelp.Show(null, VelumHelpTopics.Index);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenDxfBatchDiagnosticsForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumDxfBatchDiagnosticsFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenPdfBatchDiagnosticsForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumPdfBatchDiagnosticsFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenMaterialBatchForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumMaterialBatchFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenDocumentPropertyBatchForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumDocumentPropertyBatchFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenProductRegistryForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumProductRegistryFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenAssemblyRegistryForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumAssemblyRegistryFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OpenTechRequirementsForm()
    {
      try
      {
        var swApp = Application as ISwApplication;
        VelumTechRequirementsFormHost.TryShow(swApp);
      }
      catch (Exception ex)
      {
        MessageBox.Show(FormatExceptionForUi(ex), "Velum", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void OnVelumCommandStateResolve(Velum_Commands cmd, CommandState state)
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

      switch (cmd)
      {
        case Velum_Commands.AgentStart:
          // User: старт запрещён. Admin: пока ISIDA не поднята — «Старт» для инициализации; после — только если агент не «мертв».
          state.Enabled = isAdmin && !running && (!ready || !dead);
          break;

        case Velum_Commands.AgentStop:
          state.Enabled = ready && running;
          break;

        case Velum_Commands.ProjectSettings:
          state.Enabled = isAdmin;
          break;

        case Velum_Commands.DxfBatchDiagnostics:
          state.Enabled = isAdmin && IsActiveAssemblyDocument();
          break;

        case Velum_Commands.PdfBatchDiagnostics:
          state.Enabled = isAdmin && IsActiveAssemblyDocument();
          break;

        case Velum_Commands.MaterialBatch:
          state.Enabled = isAdmin;
          break;

        case Velum_Commands.DocumentPropertyBatch:
          state.Enabled = isAdmin;
          break;

        case Velum_Commands.ProductRegistry:
          // Реестр документов доступен и user (только чтение), и admin.
          state.Enabled = true;
          break;

        case Velum_Commands.AssemblyRegistry:
          // Реестр изделия — всем; активен при активной сборке.
          state.Enabled = IsActiveAssemblyDocument();
          break;

        case Velum_Commands.TechRequirements:
          // Тех. требования — всем; активны при чертеже (WorkspaceTypes.Drawing).
          state.Enabled = true;
          break;

        case Velum_Commands.Help:
          state.Enabled = true;
          break;
      }
    }

    private bool IsActiveAssemblyDocument()
    {
      try
      {
        var swApp = this.Application as ISwApplication;
        ModelDoc2 active = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        return active != null &&
               active.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    private void StartPulse()
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.PulseStartDeniedMessage))
        return;

      if (!VelumIsidaHost.TryInitialize(out string err))
      {
        Application.ShowMessageBox(
            "Не удалось инициализировать ISIDA:\n" + err,
            MessageBoxIcon_e.Warning,
            MessageBoxButtons_e.Ok);
        RefreshOpenSettingsEvolutionUi();
        return;
      }

      try
      {
        if (_pulse == null)
          _pulse = new VelumPulseController();

        if (_pulse.IsRunning)
        {
          RefreshOpenSettingsEvolutionUi();
          return;
        }

        if (AppGlobalState.IsDead)
        {
          Application.ShowMessageBox(
              "Агент в состоянии «мертв» — пульсацию запустить нельзя.",
              MessageBoxIcon_e.Warning,
              MessageBoxButtons_e.Ok);
          RefreshOpenSettingsEvolutionUi();
          return;
        }

        _pulse.Start();

        RefreshOpenSettingsEvolutionUi();
        (_agentTaskPane?.Control as VelumAgentTaskPane)?.RefreshVelumCommandButtons();
      }
      catch (Exception ex)
      {
        Application.ShowMessageBox(FormatExceptionForUi(ex), MessageBoxIcon_e.Error, MessageBoxButtons_e.Ok);
        RefreshOpenSettingsEvolutionUi();
      }
    }

    private void StopPulse()
    {
      if (!VelumIsidaHost.TryInitialize(out string err))
      {
        Application.ShowMessageBox(
            "Не удалось инициализировать ISIDA:\n" + err,
            MessageBoxIcon_e.Warning,
            MessageBoxButtons_e.Ok);
        RefreshOpenSettingsEvolutionUi();
        return;
      }

      try
      {
        if (_pulse == null)
          _pulse = new VelumPulseController();

        if (_pulse.IsRunning)
          _pulse.Stop();

        RefreshOpenSettingsEvolutionUi();
        (_agentTaskPane?.Control as VelumAgentTaskPane)?.RefreshVelumCommandButtons();
      }
      catch (Exception ex)
      {
        Application.ShowMessageBox(FormatExceptionForUi(ex), MessageBoxIcon_e.Error, MessageBoxButtons_e.Ok);
        RefreshOpenSettingsEvolutionUi();
      }
    }

    /// <summary>
    /// Команды ленты и контекстного меню Velum.
    /// </summary>
    /// <remarks>
    /// Явный User Id группы: при изменении набора команд увеличьте значение — иначе SolidWorks может
    /// оставить на тулбаре старую разметку (например, только Старт/Стоп без новой кнопки).
    /// </remarks>
    [CommandGroupInfo(5849214)]
    [Title("Velum")]
    private enum Velum_Commands
    {
      [Title("Старт")]
      [Description("Запустить цикл агента ISIDA с моделью.")]
      [Icon(typeof(Resources), nameof(Resources.PulseStart16))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      AgentStart = 1,

      [Title("Стоп")]
      [Description("Остановить цикл агента ISIDA.")]
      [Icon(typeof(Resources), nameof(Resources.PulseStop16))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      AgentStop = 2,

      [Title("Настройки")]
      [Description("Открыть настройки проекта.")]
      [Icon(typeof(Resources), nameof(Resources.Settings16))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      ProjectSettings = 3,

      [Title("DXF пакет")]
      [Description("Пакетная диагностика и экспорт DXF по составу активной сборки.")]
      [Icon(typeof(Resources), nameof(Resources.ExportDXF))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.Assembly,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      DxfBatchDiagnostics = 4,

      [Title("PDF пакет")]
      [Description("Пакетная диагностика и экспорт PDF по составу активной сборки.")]
      [Icon(typeof(Resources), nameof(Resources.ExportPDF))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.Assembly,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      PdfBatchDiagnostics = 5,

      [Title("Материалы пакет")]
      [Description("Пакетное присвоение материалов деталям по конфигурациям.")]
      [Icon(typeof(Resources), nameof(Resources.Material))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      MaterialBatch = 6,

      [Title("Свойства пакет")]
      [Description("Пакетное обновление свойств деталей и сборок.")]
      [Icon(typeof(Resources), nameof(Resources.EditPage))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      DocumentPropertyBatch = 7,

      [Title("Реестр документов")]
      [Description("Реестр документов: каталоги и записи с обозначением и наименованием.")]
      [Icon(typeof(Resources), nameof(Resources.Scenario))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      ProductRegistry = 8,

      [Title("Тех. требования")]
      [Description("Заполнение технических требований на чертеже.")]
      [Icon(typeof(Resources), nameof(Resources.Text))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.Drawing,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      TechRequirements = 9,

      [Title("Реестр изделия")]
      [Description("Реестр изделия: состав активной сборки.")]
      [Icon(typeof(Resources), nameof(Resources.Graf))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.Assembly,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      AssemblyRegistry = 10,

      [Title("Справка")]
      [Description("Открыть справку Velum (стартовая страница).")]
      [Icon(typeof(Resources), nameof(Resources.Help32))]
      [CommandItemInfo(
          hasMenu: true,
          hasToolbar: true,
          suppWorkspaces: WorkspaceTypes_e.AllDocuments,
          showInCmdTabBox: true,
          textStyle: RibbonTabTextDisplay_e.TextBelow)]
      Help = 11,
    }

    /// <summary>
    /// Регистрация COM-надстройки в реестре (для разработки без регистрации через regasm вручную).
    /// </summary>
    /// <param name="t">Тип надстройки.</param>
    [ComRegisterFunction]
    public static new void RegisterFunction(Type t)
    {
      string guid = t.GUID.ToString("B");

      using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\AddIns\" + guid))
      {
        key.SetValue("", 1, RegistryValueKind.DWord);
        key.SetValue("Title", "Velum");
        key.SetValue("Description", "Velum");
      }

      using (RegistryKey cu = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\" + guid))
      {
        cu.SetValue("", 1, RegistryValueKind.DWord);
      }
    }

    /// <summary>
    /// Удаление записей реестра надстройки.
    /// </summary>
    /// <param name="t">Тип надстройки.</param>
    [ComUnregisterFunction]
    public static new void UnregisterFunction(Type t)
    {
      string guid = t.GUID.ToString("B");

      Registry.LocalMachine.DeleteSubKey(
          @"SOFTWARE\SolidWorks\AddIns\" + guid,
          throwOnMissingSubKey: false);

      Registry.CurrentUser.DeleteSubKey(
          @"Software\SolidWorks\AddInsStartup\" + guid,
          throwOnMissingSubKey: false);
    }
  }
}
