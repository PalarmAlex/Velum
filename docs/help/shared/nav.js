/* Единое древовидное меню справки Velum (IE / WebBrowser). */
(function () {
  /* ---- Версия справки: менять только здесь ---- */
  var VELUM_HELP_VERSION = "V1.0";
  /* --------------------------------------------- */

  function qs(id) { return document.getElementById(id); }

  function fileName(href) {
    if (!href) return "";
    var i = href.lastIndexOf("/");
    var name = i >= 0 ? href.substring(i + 1) : href;
    var q = name.indexOf("?");
    if (q >= 0) name = name.substring(0, q);
    var h = name.indexOf("#");
    if (h >= 0) name = name.substring(0, h);
    return name.toLowerCase();
  }

  function currentFile() {
    try {
      var path = window.location.pathname || "";
      path = decodeURIComponent(path.replace(/\\/g, "/"));
      var i = path.lastIndexOf("/");
      return (i >= 0 ? path.substring(i + 1) : path).toLowerCase();
    } catch (e) {
      return "";
    }
  }

  function toggleGroup(ev) {
    if (ev && ev.preventDefault) ev.preventDefault();
    var a = ev.currentTarget || ev.srcElement;
    while (a && a.tagName !== "A") a = a.parentNode;
    if (!a) return false;
    var li = a.parentNode;
    if (!li) return false;
    var open = li.className.indexOf("open") >= 0;
    li.className = open ? "nav-group" : "nav-group open";
    return false;
  }

  function build(base) {
    return [
      { title: "Общее", items: [
        { t: "Оглавление", h: "index.html" },
        { t: "Обзор", h: "overview.html" },
        { t: "ISIDA и МВАП", h: "platform.html" },
        { t: "Лента команд", h: "ribbon.html" },
        { t: "Фильтры списков", h: "shared/list-filters.html" }
      ]},
      { title: "Основные формы", items: [
        { t: "Настройки проекта", h: "forms/project-settings.html" },
        { t: "Реестр изделия", h: "forms/assembly-registry.html" },
        { t: "Столбцы реестра", h: "forms/assembly-columns.html" },
        { t: "Отчёты реестра", h: "forms/assembly-reports.html" },
        { t: "Реестр документов", h: "forms/product-registry.html" },
        { t: "Карточка изделия", h: "forms/product-item.html" },
        { t: "Проблемы реестра", h: "forms/product-problems.html" },
        { t: "Тех. требования", h: "forms/tech-requirements.html" },
        { t: "Материалы пакет", h: "forms/material-batch.html" },
        { t: "Свойства пакет", h: "forms/document-properties.html" }
      ]},
      { title: "Экспорт", items: [
        { t: "DXF пакет", h: "forms/dxf-batch.html" },
        { t: "Экспорт DXF", h: "forms/dxf-export.html" },
        { t: "Суффиксы DXF", h: "forms/dxf-suffixes.html" },
        { t: "PDF пакет", h: "forms/pdf-batch.html" },
        { t: "Экспорт PDF", h: "forms/pdf-export.html" }
      ]},
      { title: "Агент", items: [
        { t: "Панель «Агент»", h: "forms/agent-taskpane.html" },
        { t: "Метрики среды", h: "forms/environment-metrics.html" },
        { t: "Воздействия оператора", h: "forms/operator-influences.html" },
        { t: "Буфер команд", h: "forms/sensor-buffer.html" }
      ]}
    ];
  }

  function injectVersion(base) {
    var nodes = document.getElementsByClassName("content");
    if (!nodes || nodes.length === 0) return;
    var content = nodes[0];
    var old = document.getElementById("velum-help-version");
    if (old && old.parentNode) old.parentNode.removeChild(old);

    var footer = document.createElement("p");
    footer.id = "velum-help-version";
    footer.className = "page-version";

    var link = document.createElement("a");
    link.href = (base || "") + "index.html";
    link.appendChild(document.createTextNode("Версия справки " + VELUM_HELP_VERSION));
    footer.appendChild(link);
    content.appendChild(footer);
  }

  function render() {
    var host = qs("velum-nav");
    if (!host) return;
    var base = host.getAttribute("data-base") || "";
    var cur = currentFile();
    var groups = build(base);
    var html = [];
    html.push('<div class="brand"><a href="' + base + 'index.html">Справка Velum</a></div>');
    html.push('<ul class="nav-tree">');

    for (var g = 0; g < groups.length; g++) {
      var group = groups[g];
      var itemsHtml = [];
      for (var i = 0; i < group.items.length; i++) {
        var it = group.items[i];
        var href = base + it.h;
        var isCur = fileName(it.h) === cur;
        itemsHtml.push('<li><a' + (isCur ? ' class="current"' : '') + ' href="' + href + '">' + it.t + '</a></li>');
      }
      html.push('<li class="nav-group open">');
      html.push('<a href="#" class="nav-toggle">' + group.title + '</a>');
      html.push('<ul>' + itemsHtml.join("") + '</ul>');
      html.push('</li>');
    }
    html.push('</ul>');
    host.innerHTML = html.join("");

    var toggles = host.getElementsByTagName("a");
    for (var t = 0; t < toggles.length; t++) {
      if (toggles[t].className.indexOf("nav-toggle") >= 0) {
        toggles[t].onclick = toggleGroup;
      }
    }

    injectVersion(base);
  }

  if (document.addEventListener)
    document.addEventListener("DOMContentLoaded", render, false);
  else if (window.attachEvent)
    window.attachEvent("onload", render);
  else
    render();
})();
