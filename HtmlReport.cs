using System.Text.Json;

namespace D2RItemInspector;

/// <summary>Writes a self-contained HTML page (inline CSS+JS, data embedded) of all equipment items
/// with client-side filtering. Open it from disk; re-run the app and reload to refresh.</summary>
public static class HtmlReport
{
    public static void Write(InspectionResult result, string path)
    {
        var rows = ItemReport.Build(result);
        WikiLinker.Annotate(rows, WikiCachePath());
        string json = JsonSerializer.Serialize(rows);
        string html = Template
            .Replace("__DATA__", json)
            .Replace("__GENERATED__", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        File.WriteAllText(path, html);
    }

    // The wiki-link existence cache is an internal artifact, so keep it in the per-user cache
    // location (%LOCALAPPDATA%\D2RItemInspector) instead of cluttering the exe's directory.
    private static string WikiCachePath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir)) baseDir = Path.GetTempPath();
        string dir = Path.Combine(baseDir, "D2RItemInspector");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "wiki-link-cache.json");
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Diablo II Resurrected - Offline Item Browser</title>
<style>
  body { background:#1a1a1a; color:#ddd; font-family:'Segoe UI',Arial,sans-serif; margin:0; padding:16px; }
  h1 { font-size:18px; margin:0 0 2px; }
  .subtitle { color:#b08d57; font-size:13px; margin:0 0 8px; font-style:italic; }
  .meta { color:#888; font-size:12px; margin-bottom:10px; }
  .legend span { margin-right:12px; font-size:12px; }
  .filters { display:flex; flex-wrap:wrap; gap:10px; align-items:flex-end; margin:10px 0; background:#232323; padding:12px; border-radius:6px; }
  .filters label { display:flex; flex-direction:column; font-size:11px; color:#9a9a9a; gap:3px; }
  .filters input, .filters select { background:#2c2c2c; color:#eee; border:1px solid #444; border-radius:4px; padding:4px 6px; font-size:13px; }
  .filters input[type=number] { width:60px; }
  .filters .chk { flex-direction:row; align-items:center; gap:5px; font-size:13px; color:#ddd; }
  .count { color:#888; font-size:12px; margin:6px 0; }
  table { border-collapse:collapse; width:100%; font-size:13px; }
  th,td { text-align:left; padding:5px 9px; border-bottom:1px solid #333; white-space:nowrap; }
  th { position:sticky; top:0; background:#262626; cursor:pointer; user-select:none; }
  th.sorted::after { content:" \2195"; color:#777; }
  a.wiki { color:inherit; text-decoration:none; }
  a.wiki:hover { text-decoration:underline; }
  tbody tr:hover { background:#242424; }
  .white{color:#fff}.gray{color:#9c9c9c}.magic{color:#6f6fff}.rare{color:#ffff64}.set{color:#22d422}.unique{color:#c7b377}.crafted{color:#ffa800}
  .sock { color:#8ab4ff; cursor:help; }
  .muted { color:#5a5a5a; }
  .ethsfx { font-style:italic; opacity:.8; }
  td[data-tip] { cursor:help; }
  .tip { position:absolute; display:none; z-index:50; background:#0c0c0c; border:1px solid #555; border-radius:5px; padding:10px 14px; font-size:14px; line-height:1.5; color:#cfcfcf; pointer-events:none; max-width:460px; box-shadow:0 5px 18px rgba(0,0,0,.7); }
  .tip div { white-space:nowrap; }
  .tip .setb { color:#22d422; }
  .tip .tiphead { font-weight:600; margin-bottom:3px; }
  .tip .tipfull { color:#22d422; }
  .tip .tippart { color:#ffff64; }
  .tip .tipmiss { color:#d9a066; }
  .tip .tipital { font-style:italic; color:#999; }
  .tip .tipsub { font-weight:600; color:#9a8fd6; margin-top:5px; }
  .tip .tipdiv { border-top:1px solid #444; margin-top:6px; padding-top:5px; }
  td.setlink { cursor:pointer; }
  td.setlink:hover { text-decoration:underline; filter:brightness(1.25); }
  .count a { color:#7fb2ff; text-decoration:none; margin-left:4px; }
  .count a:hover { text-decoration:underline; }
  .fitem { display:flex; flex-direction:column; font-size:11px; color:#9a9a9a; gap:3px; }
  details.multi > summary { list-style:none; cursor:pointer; background:#2c2c2c; border:1px solid #444; border-radius:4px; padding:4px 8px; font-size:13px; color:#eee; min-width:96px; }
  details.multi > summary::-webkit-details-marker { display:none; }
  details.multi > summary::after { content:" \25BE"; color:#888; }
  details.multi[open] > summary { border-color:#666; }
  details.multi { position:relative; }
  details.multi .opts { position:absolute; z-index:10; background:#2c2c2c; border:1px solid #555; border-radius:4px; padding:6px 20px 6px 8px; margin-top:3px; max-height:300px; overflow-x:hidden; overflow-y:auto; width:max-content; box-shadow:0 4px 14px rgba(0,0,0,.5); }
  details.multi .opts label { display:flex; flex-direction:row; align-items:center; gap:6px; padding:2px 4px; white-space:nowrap; color:#ddd; font-size:13px; }
  details.multi .opts label.master { font-weight:600; }
  details.multi .opts label.allw { border-bottom:1px solid #444; margin-bottom:3px; padding-bottom:4px; font-weight:600; }
</style>
</head>
<body>
<h1>Diablo II: Resurrected &mdash; Offline Item Browser</h1>
<div class="subtitle">Supports Return of the Warlock expansion</div>
<div class="meta">Generated __GENERATED__ &middot; data is a snapshot &mdash; re-run the app and reload (F5) to refresh.</div>
<div class="legend">
  <span class="white">White</span><span class="gray">Gray (socketed/eth/runeword)</span>
  <span class="magic">Magic</span><span class="rare">Rare</span><span class="set">Set</span>
  <span class="unique">Unique</span><span class="crafted">Crafted</span>
</div>
<div class="filters">
  <label>Name<input id="f-name" type="text" placeholder="search…"></label>
  <div class="fitem"><span>Rarity</span><details class="multi"><summary>Any</summary><div id="f-rarity" class="opts"></div></details></div>
  <div class="fitem"><span>Type</span><details class="multi"><summary>Any</summary><div id="f-type" class="opts"></div></details></div>
  <div class="fitem"><span>Base quality</span><details class="multi"><summary>Any</summary><div id="f-quality" class="opts"></div></details></div>
  <div class="fitem"><span>Features</span><details class="multi"><summary>Any</summary><div id="f-features" class="opts"></div></details></div>
  <label>Usable by class<select id="f-class"></select></label>
  <label>Location<select id="f-owner"></select></label>
  <label>Required lvl<input id="f-lmax" type="number" min="0"></label>
  <label class="chk"><input id="f-rw" type="checkbox">Runewords only</label>
  <label class="chk"><input id="f-eth" type="checkbox">Ethereal only</label>
  <label class="chk"><input id="f-emptysock" type="checkbox">Items with empty sockets</label>
  <label class="chk"><input id="f-dup" type="checkbox">Duplicate uniques/sets</label>
  <label class="chk"><button id="f-reset" type="button">Reset</button></label>
</div>
<div class="count" id="count"></div>
<table>
  <thead><tr>
    <th data-key="Name">Name</th><th data-key="Owner">Location</th><th data-key="Type">Type</th>
    <th data-key="Base">Base</th><th data-key="BaseQuality">Quality</th><th data-key="Set">Set</th>
    <th data-key="Sockets">Sock</th><th data-key="ReqLevel">Lvl</th><th data-key="ReqStr">Str</th>
    <th data-key="ReqDex">Dex</th><th data-key="Class">Class</th>
  </tr></thead>
  <tbody id="rows"></tbody>
</table>
<div id="tip" class="tip"></div>
<script>
const DATA = __DATA__;
const $ = id => document.getElementById(id);
const esc = s => (s==null?'':String(s)).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));

const CLASSES = ["Amazon","Sorceress","Necromancer","Paladin","Barbarian","Druid","Assassin","Warlock"];
const RARITIES = ["Normal","Magic","Rare","Crafted","Set","Unique"];
const QUALITIES = ["Normal","Exceptional","Elite"];
const FEATURES = ["+ All Attributes","+ to Dexterity","+ to Life","+ to Mana","+ to Strength","All Resistances","Better Chance of Getting Magic Items","Cannot Be Frozen","Chance of Crushing Blow","Chance of Open Wounds","Cold Resist","Faster Cast Rate","Faster Hit Recovery","Faster Run/Walk","Fire Resist","Increased Attack Speed","Indestructible","Life Stolen per Hit","Lightning Resist","Poison Resist","Slain Monsters Rest in Peace"];
const distinct = key => [...new Set(DATA.map(r => r[key]).filter(v => v!=null && v!==''))].sort();

// Items you own 2+ copies of, among uniques/sets only. Keyed on name+type+rarity so that two
// different items sharing a name (e.g. unique "Raven Claw" weapon vs crafted "Raven Claw" gloves)
// are not treated as duplicates.
const dupKey = r => r.Name + ' ' + r.Type + ' ' + r.Rarity;
const DUP_KEYS = (() => {
  const c = {};
  DATA.forEach(r => { if (r.Rarity==='Unique' || r.Rarity==='Set') { const k = dupKey(r); c[k] = (c[k]||0) + 1; } });
  return new Set(Object.keys(c).filter(k => c[k] >= 2));
})();

function fillSelect(sel, values, allLabel){
  sel.innerHTML = '<option value="">'+allLabel+'</option>' + values.map(v => '<option>'+esc(v)+'</option>').join('');
}
function fillMulti(id, values){
  $(id).innerHTML = values.map(v => '<label><input type="checkbox" value="'+esc(v)+'">'+esc(v)+'</label>').join('');
  $(id).addEventListener('change', () => { updateSummary(id); render(); });
}
function updateSummary(id){
  const box = $(id);
  const n = box.querySelectorAll('input[value]:checked').length;
  box.parentElement.querySelector('summary').textContent = n===0 ? 'Any' : n+' selected';
}
// Only real option checkboxes carry a value attribute (the "All weapons" toggle has none).
const checked = id => [...$(id).querySelectorAll('input[value]:checked')].map(i => i.value);

// Weapon Type values, for the "All weapons" master toggle in the Type filter.
const WEAPON_TYPES = ["Sword","Axe","Mace","Scepter","Polearm","Spear","Javelin","Bow","Crossbow","Dagger","Wand","Staff","Orb","Claw","Throwing","Weapon"];
function fillTypeFilter(values){
  const box = $('f-type');
  const hasWeapons = values.some(v => WEAPON_TYPES.includes(v));
  let html = '<label class="'+(hasWeapons?'master':'allw')+'"><input type="checkbox" id="f-type-any">Any</label>';
  if (hasWeapons) html += '<label class="allw"><input type="checkbox" id="f-type-allw">All weapons</label>';
  html += values.map(v => '<label><input type="checkbox" value="'+esc(v)+'"'+(WEAPON_TYPES.includes(v)?' data-weapon="1"':'')+'>'+esc(v)+'</label>').join('');
  box.innerHTML = html;
  const any = $('f-type-any'), allw = $('f-type-allw');
  const optBoxes = () => [...box.querySelectorAll('input[value]')];
  const weaponBoxes = () => [...box.querySelectorAll('input[data-weapon]')];
  box.addEventListener('change', e => {
    if (e.target === any) { optBoxes().forEach(b => b.checked = any.checked); if (allw) allw.checked = any.checked; }
    else if (e.target === allw) { weaponBoxes().forEach(b => b.checked = allw.checked); any.checked = optBoxes().every(b => b.checked); }
    else { any.checked = optBoxes().every(b => b.checked); if (allw) allw.checked = weaponBoxes().every(b => b.checked); }
    updateSummary('f-type'); render();
  });
}

fillMulti('f-rarity', RARITIES.filter(r => DATA.some(d => d.Rarity===r)));
fillTypeFilter(distinct('Type'));
fillMulti('f-quality', QUALITIES.filter(q => DATA.some(d => d.BaseQuality===q)));
fillMulti('f-features', FEATURES);
fillSelect($('f-class'), CLASSES, 'Any');
fillSelect($('f-owner'), distinct('Owner'), 'All');

let sortKey = 'Name', sortDir = 1, setFilter = '';

function rowHtml(r){
  const emptyN = Math.max(0, r.Sockets-(r.SocketItems||[]).length);
  const sockTip = (r.SocketItems||[]).map(s => ({Text:s})).concat(Array(emptyN).fill(0).map(() => ({Text:'empty', Italic:true})));
  const sockCell = r.Sockets>0
    ? '<td data-tip="'+esc(JSON.stringify(sockTip))+'"><span class="sock">'+r.Sockets+'</span></td>'
    : '<td><span class="muted">&mdash;</span></td>';
  const cell = v => '<td>'+(v===''||v==null?'<span class="muted">&mdash;</span>':esc(v))+'</td>';
  const nmText = r.WikiUrl
    ? '<a class="wiki" href="'+esc(r.WikiUrl)+'" target="_blank" rel="noopener">'+esc(r.Name)+'</a>'
    : esc(r.Name);
  const nm = nmText + (r.Eth ? ' <span class="ethsfx">(eth)</span>' : '');
  const tip = (r.Stats && r.Stats.length) ? ' data-tip="'+esc(JSON.stringify(r.Stats))+'"' : '';
  let setCell;
  if (r.SetSize > 0) {
    const lines = [{Text:'('+(r.SetOwned||0)+' / '+r.SetSize+' Items)', Head:true, Full:(r.SetOwned||0)>=r.SetSize}];
    if (r.SetMissing && r.SetMissing.length) {
      lines.push({Text:'Missing:', Miss:true});
      r.SetMissing.forEach(n => lines.push({Text:'• '+n, Miss:true}));
    }
    (r.SetBonuses||[]).forEach((t,i) => {
      const o = /:$/.test(t) ? {Text:t, Bhead:true} : {Text:t};
      if (i===0) o.Div = true; // divider above the first bonus, below the (x/n) + missing block
      lines.push(o);
    });
    setCell = '<td class="set setlink" data-set="'+esc(r.Set)+'" data-tip="'+esc(JSON.stringify(lines))+'">'+esc(r.Set)+'</td>';
  } else setCell = cell(r.Set);
  return '<tr>'
    + '<td class="'+esc(r.Color)+'"'+tip+'>'+nm+'</td>'
    + '<td>'+esc(r.Owner)+' &middot; <span class="muted">'+esc(r.Source)+'</span></td>'
    + cell(r.Type) + cell(r.Base) + cell(r.BaseQuality) + setCell
    + sockCell
    + cell(r.ReqLevel||'') + cell(r.ReqStr||'') + cell(r.ReqDex||'')
    + cell(r.Class)
    + '</tr>';
}

function render(){
  const name = $('f-name').value.trim().toLowerCase();
  const rar = new Set(checked('f-rarity'));
  const typ = new Set(checked('f-type'));
  const qua = new Set(checked('f-quality'));
  const feat = checked('f-features');
  const cls = $('f-class').value, owner = $('f-owner').value;
  const lmax = $('f-lmax').value==='' ? Infinity : parseInt($('f-lmax').value);
  const rwOnly = $('f-rw').checked, ethOnly = $('f-eth').checked, dupOnly = $('f-dup').checked;
  const emptySock = $('f-emptysock').checked;

  let out = DATA.filter(r =>
    (!setFilter || r.Set===setFilter) &&
    (!name || r.Name.toLowerCase().includes(name)) &&
    (rar.size===0 || rar.has(r.Rarity)) &&
    (typ.size===0 || typ.has(r.Type)) &&
    (qua.size===0 || qua.has(r.BaseQuality)) &&
    (feat.length===0 || feat.every(f => (r.Features||[]).includes(f))) &&
    (!cls || !r.Class || r.Class===cls) &&
    (!owner || r.Owner===owner) &&
    (r.ReqLevel<=lmax) &&
    (!rwOnly || r.Runeword) &&
    (!ethOnly || r.Eth) &&
    (!emptySock || (r.Sockets>0 && (r.SocketItems||[]).length===0)) &&
    (!dupOnly || DUP_KEYS.has(dupKey(r))));

  out.sort((a,b) => {
    let x=a[sortKey], y=b[sortKey];
    if (typeof x==='number' && typeof y==='number') return (x-y)*sortDir;
    return String(x??'').localeCompare(String(y??''))*sortDir;
  });

  $('rows').innerHTML = out.map(rowHtml).join('');
  $('count').innerHTML = out.length + ' / ' + DATA.length + ' items'
    + (setFilter ? ' &middot; set: <b>'+esc(setFilter)+'</b> <a href="#" id="clearset">&times; clear</a>' : '');
}

document.querySelectorAll('th[data-key]').forEach(th => th.addEventListener('click', () => {
  const k = th.dataset.key;
  sortDir = (sortKey===k) ? -sortDir : 1;
  sortKey = k;
  document.querySelectorAll('th').forEach(t => t.classList.remove('sorted'));
  th.classList.add('sorted');
  render();
}));
['f-name','f-class','f-owner','f-lmax','f-rw','f-eth','f-emptysock','f-dup'].forEach(id => $(id).addEventListener('input', render));
// Click a set name to filter to that set (click it again, or the clear link, to remove the filter).
$('rows').addEventListener('click', e => {
  const td = e.target.closest('td.setlink');
  if (!td) return;
  setFilter = (setFilter === td.dataset.set) ? '' : td.dataset.set;
  render();
});
$('count').addEventListener('click', e => {
  if (e.target.id === 'clearset') { e.preventDefault(); setFilter = ''; render(); }
});
$('f-reset').addEventListener('click', () => {
  setFilter = '';
  document.querySelectorAll('.filters input').forEach(el => { if (el.type==='checkbox') el.checked=false; else el.value=''; });
  $('f-class').value=''; $('f-owner').value='';
  ['f-rarity','f-type','f-quality','f-features'].forEach(id => { $(id).querySelectorAll('input').forEach(i => i.checked=false); updateSummary(id); });
  render();
});

// Only one multi-select dropdown open at a time; close all when clicking outside them.
document.querySelectorAll('details.multi').forEach(d => d.addEventListener('toggle', () => {
  if (d.open) document.querySelectorAll('details.multi[open]').forEach(o => { if (o !== d) o.open = false; });
}));
document.addEventListener('click', e => {
  if (!e.target.closest('details.multi')) document.querySelectorAll('details.multi[open]').forEach(o => o.open = false);
});

// Floating stats tooltip on hovering an item name.
const tipEl = $('tip'), rowsEl = $('rows');
rowsEl.addEventListener('mouseover', e => {
  const td = e.target.closest('td[data-tip]');
  if (td) {
    const lines = JSON.parse(td.dataset.tip);
    tipEl.innerHTML = lines.map(s => {
      let c = s.Head ? 'tiphead '+(s.Full?'tipfull':'tippart') : s.Bhead ? 'tipsub' : s.Miss ? 'tipmiss' : s.Italic ? 'tipital' : s.Set ? 'setb' : '';
      if (s.Div) c = (c ? c+' ' : '')+'tipdiv';
      return '<div'+(c?' class="'+c+'"':'')+'>'+esc(s.Text)+'</div>';
    }).join('');
    tipEl.style.display = 'block';
  } else tipEl.style.display = 'none';
});
rowsEl.addEventListener('mousemove', e => {
  if (tipEl.style.display === 'block') { tipEl.style.left = (e.pageX + 14) + 'px'; tipEl.style.top = (e.pageY + 14) + 'px'; }
});
rowsEl.addEventListener('mouseleave', () => tipEl.style.display = 'none');

render();
</script>
</body>
</html>
""";
}
