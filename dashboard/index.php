<?php
if (isset($_GET['phpinfo'])) {
    phpinfo();
    exit;
}

$serverSoftware = $_SERVER['SERVER_SOFTWARE'] ?? 'Apache';
$apacheVer = 'Unknown';
if (preg_match('/Apache\/([0-9.]+)/', $serverSoftware, $m)) {
    $apacheVer = $m[1];
}
$phpVer = phpversion();
$hostName = $_SERVER['HTTP_HOST'] ?? 'solstice.local';

// Quick non-blocking socket check for MySQL (3306) and MariaDB (3307)
function checkPort($port) {
    $fp = @fsockopen('127.0.0.1', $port, $errno, $errstr, 0.15);
    if ($fp) {
        fclose($fp);
        return true;
    }
    return false;
}

$mysqlUp = checkPort(3306);
$mariadbUp = checkPort(3307);

// Scan local projects in C:\wamp64\www
$ignore = ['.', '..', 'wamplangues', 'wampthemes'];
$projects = [];
foreach (scandir(__DIR__) as $item) {
    if (in_array($item, $ignore, true)) continue;
    $fullPath = __DIR__ . DIRECTORY_SEPARATOR . $item;
    if (is_dir($fullPath)) {
        $isLink = is_link($fullPath);
        $mtime = @filemtime($fullPath) ?: 0;
        $projects[] = [
            'name' => $item,
            'isLink' => $isLink,
            'mtime' => $mtime,
        ];
    }
}
usort($projects, function($a, $b) {
    return strcasecmp($a['name'], $b['name']);
});
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>SOLSTICE // Local Command</title>
  <link rel="icon" type="image/svg+xml" href="/solstice-icon.svg" />
  <style>
    :root {
      --bg: #050811;
      --surface: #0A101F;
      --surface-hover: #0F172A;
      --border: #172542;
      --border-bright: #2563EB;
      --accent: #38BDF8;
      --accent-dim: rgba(56, 189, 248, 0.12);
      --text: #E2E8F0;
      --muted: #64748B;
      --ok: #22C55E;
      --off: #475569;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background:
        radial-gradient(circle at 85% 0%, rgba(29, 78, 216, 0.16) 0%, transparent 45%),
        radial-gradient(circle at 10% 100%, rgba(56, 189, 248, 0.08) 0%, transparent 40%),
        var(--bg);
      color: var(--text);
      font-family: 'Segoe UI', Inter, system-ui, -apple-system, sans-serif;
      min-height: 100vh;
      padding: 36px 28px 60px;
      letter-spacing: 0.01em;
    }
    .container { max-width: 1220px; margin: 0 auto; }

    /* Header */
    .topbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 20px;
      padding-bottom: 24px;
      border-bottom: 1px solid var(--border);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .brand-mark {
      width: 46px;
      height: 46px;
      border: 1px solid var(--border-bright);
      background: #070C18;
      border-radius: 10px;
      display: grid;
      place-items: center;
      box-shadow: 0 0 24px rgba(37, 99, 235, 0.25);
    }
    .brand-text h1 {
      font-size: 1.55rem;
      font-weight: 800;
      letter-spacing: 0.26em;
      text-transform: uppercase;
      color: #F8FAFC;
    }
    .brand-badge {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      margin-top: 4px;
      font-size: 0.72rem;
      font-weight: 600;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--accent);
    }
    .brand-badge .dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: var(--accent);
      box-shadow: 0 0 10px var(--accent);
    }

    /* Quick Actions */
    .actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
    }
    .btn {
      text-decoration: none;
      color: var(--text);
      background: var(--surface);
      border: 1px solid var(--border);
      padding: 9px 15px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      transition: all 0.16s ease;
    }
    .btn:hover {
      border-color: var(--accent);
      color: var(--accent);
      background: var(--surface-hover);
      box-shadow: 0 0 16px rgba(56, 189, 248, 0.15);
    }
    .btn-primary {
      background: rgba(37, 99, 235, 0.18);
      border-color: rgba(56, 189, 248, 0.45);
      color: #BAE6FD;
    }

    /* Telemetry Strip */
    .telemetry {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 12px;
      margin: 22px 0 28px;
    }
    .stat {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 14px 16px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .stat-label {
      font-size: 0.7rem;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--muted);
      margin-bottom: 4px;
    }
    .stat-val {
      font-size: 0.96rem;
      font-weight: 700;
      color: #F1F5F9;
      font-family: Consolas, 'Cascadia Code', monospace;
    }
    .pill {
      font-size: 0.68rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      padding: 4px 8px;
      border-radius: 4px;
      text-transform: uppercase;
    }
    .pill-on {
      color: #4ADE80;
      background: rgba(34, 197, 94, 0.12);
      border: 1px solid rgba(34, 197, 94, 0.3);
    }
    .pill-off {
      color: #94A3B8;
      background: rgba(71, 85, 105, 0.15);
      border: 1px solid rgba(71, 85, 105, 0.3);
    }

    /* Search + Count */
    .section-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 14px;
      margin-bottom: 16px;
    }
    .section-title {
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.18em;
      text-transform: uppercase;
      color: #94A3B8;
    }
    .search-box {
      background: var(--surface);
      border: 1px solid var(--border);
      color: var(--text);
      padding: 9px 14px;
      border-radius: 6px;
      width: 280px;
      max-width: 100%;
      font-size: 0.85rem;
      outline: none;
      transition: border-color 0.15s ease;
    }
    .search-box:focus {
      border-color: var(--accent);
      box-shadow: 0 0 12px rgba(56, 189, 248, 0.15);
    }

    /* Project Grid */
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(235px, 1fr));
      gap: 12px;
    }
    .card {
      position: relative;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 10px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 15px 16px;
      text-decoration: none;
      color: var(--text);
      transition: transform 0.14s ease, border-color 0.14s ease, background 0.14s ease;
    }
    .card:hover {
      transform: translateY(-2px);
      border-color: var(--border-bright);
      background: var(--surface-hover);
      box-shadow: 0 8px 24px rgba(2, 6, 23, 0.7);
    }
    .card-top {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 8px;
    }
    .card-name {
      font-weight: 700;
      font-size: 0.94rem;
      color: #F8FAFC;
      word-break: break-word;
    }
    .card-tag {
      font-size: 0.62rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      text-transform: uppercase;
      color: var(--accent);
      background: var(--accent-dim);
      padding: 2px 6px;
      border-radius: 3px;
      flex-shrink: 0;
    }
    .card-url {
      font-size: 0.74rem;
      font-family: Consolas, monospace;
      color: var(--muted);
    }
    .card:hover .card-url { color: var(--accent); }

    .footer {
      margin-top: 38px;
      padding-top: 16px;
      border-top: 1px solid var(--border);
      display: flex;
      justify-content: space-between;
      font-size: 0.72rem;
      color: var(--muted);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
  </style>
</head>
<body>
  <div class="container">
    <header class="topbar">
      <div class="brand">
        <div class="brand-mark">
          <svg width="26" height="26" viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg">
            <circle cx="32" cy="32" r="20" fill="none" stroke="#38BDF8" stroke-width="3.5"/>
            <circle cx="38" cy="28" r="16" fill="#070C18"/>
            <line x1="8" y1="32" x2="56" y2="32" stroke="#38BDF8" stroke-width="2" stroke-opacity="0.6"/>
          </svg>
        </div>
        <div class="brand-text">
          <h1>Solstice</h1>
          <div class="brand-badge"><span class="dot"></span> Local Command // <?= htmlspecialchars($hostName) ?></div>
        </div>
      </div>

      <div class="actions">
        <a class="btn btn-primary" href="/phpmyadmin/" target="_blank">phpMyAdmin</a>
        <a class="btn" href="/?phpinfo=1" target="_blank">PHP Info</a>
        <a class="btn" href="/add_vhost.php">VirtualHosts</a>
      </div>
    </header>

    <section class="telemetry">
      <div class="stat">
        <div>
          <div class="stat-label">Web Engine</div>
          <div class="stat-val">Apache <?= htmlspecialchars($apacheVer) ?></div>
        </div>
        <span class="pill pill-on">Active</span>
      </div>
      <div class="stat">
        <div>
          <div class="stat-label">PHP Runtime</div>
          <div class="stat-val">PHP <?= htmlspecialchars($phpVer) ?></div>
        </div>
        <span class="pill pill-on">Ready</span>
      </div>
      <div class="stat">
        <div>
          <div class="stat-label">MySQL (:3306)</div>
          <div class="stat-val"><?= $mysqlUp ? '127.0.0.1:3306' : 'Offline' ?></div>
        </div>
        <span class="pill <?= $mysqlUp ? 'pill-on' : 'pill-off' ?>"><?= $mysqlUp ? 'Online' : 'Idle' ?></span>
      </div>
      <div class="stat">
        <div>
          <div class="stat-label">MariaDB (:3307)</div>
          <div class="stat-val"><?= $mariadbUp ? '127.0.0.1:3307' : 'Offline' ?></div>
        </div>
        <span class="pill <?= $mariadbUp ? 'pill-on' : 'pill-off' ?>"><?= $mariadbUp ? 'Online' : 'Idle' ?></span>
      </div>
    </section>

    <div class="section-head">
      <div class="section-title">Workspaces // <span id="projCount"><?= count($projects) ?></span> Directories</div>
      <input class="search-box" id="filterInput" type="text" placeholder="Filter projects..." autocomplete="off" />
    </div>

    <section class="grid" id="projectGrid">
      <?php foreach ($projects as $p): ?>
        <a class="card" href="/<?= rawurlencode($p['name']) ?>/" data-name="<?= htmlspecialchars(strtolower($p['name'])) ?>">
          <div class="card-top">
            <div class="card-name"><?= htmlspecialchars($p['name']) ?></div>
            <?php if ($p['isLink']): ?>
              <span class="card-tag">Link</span>
            <?php else: ?>
              <span class="card-tag">App</span>
            <?php endif; ?>
          </div>
          <div class="card-url">/<?= htmlspecialchars($p['name']) ?>/</div>
        </a>
      <?php endforeach; ?>
    </section>

    <footer class="footer">
      <span>Solstice Local Stack</span>
      <span>Root: <?= htmlspecialchars(__DIR__) ?></span>
    </footer>
  </div>

  <script>
    const input = document.getElementById('filterInput');
    const cards = Array.from(document.querySelectorAll('#projectGrid .card'));
    const countEl = document.getElementById('projCount');
    input.addEventListener('input', () => {
      const q = input.value.trim().toLowerCase();
      let visible = 0;
      for (const c of cards) {
        const match = c.dataset.name.includes(q);
        c.style.display = match ? 'flex' : 'none';
        if (match) visible++;
      }
      countEl.textContent = visible;
    });
  </script>
</body>
</html>
