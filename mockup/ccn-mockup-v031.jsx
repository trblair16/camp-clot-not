import { useState, useEffect, useRef } from "react";

// ─── THEME ───────────────────────────────────────────────────────────────────
// All theme tokens in one place — swap for dark/neutral themes later
const THEME = {
  bg:        "#F2ECD8",   // cream base
  bgDot:     "#DDD5BE",   // dot grid color
  black:     "#1A1A1A",   // all outlines & text strokes
  red:       "#D12B2B",
  blue:      "#2563C4",
  green:     "#2B8A3E",
  yellow:    "#F5C800",
  white:     "#FFFFFF",
  textDark:  "#1A1A1A",
  textMid:   "#4A4035",
  textLight: "#8A7D6A",
  // Panel chrome
  panelBg:   "#FFFEF7",
  panelBorder: "#1A1A1A",
  panelShadow: "4px 4px 0px #1A1A1A",
  // Rank highlight
  rank1Bg:   "#F5C800",
  rank1Text: "#1A1A1A",
};

// ─── DATA ────────────────────────────────────────────────────────────────────
const GROUPS = [
  { id:1, name:"Mario's Mushroom Crew",  short:"Mushroom",  avatar:"🍄", color:THEME.red,    boardPos:3  },
  { id:2, name:"Luigi's Green Machine",  short:"Green",     avatar:"🌿", color:THEME.green,  boardPos:6  },
  { id:3, name:"Peach's Power Squad",    short:"Peach",     avatar:"👑", color:THEME.blue,   boardPos:10 },
  { id:4, name:"Toad's Toadstool Tribe", short:"Toadstool", avatar:"⭐", color:THEME.yellow, boardPos:5  },
];

const SCRIPTED_DESTINATIONS = { 1:7, 2:11, 3:2, 4:9 };

const BOARD_SPACES = [
  { id:0,  type:"start",    label:"START",      icon:"🏁", color:THEME.green,  x:60,  y:385 },
  { id:1,  type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:130, y:385 },
  { id:2,  type:"minigame", label:"Mini-Game",  icon:"🎮", color:THEME.blue,   x:200, y:385 },
  { id:3,  type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:270, y:385 },
  { id:4,  type:"bowser",   label:"Bowser!",    icon:"👹", color:THEME.red,    x:340, y:385 },
  { id:5,  type:"star",     label:"Star Space", icon:"⭐", color:THEME.red,    x:410, y:385 },
  { id:6,  type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:480, y:385 },
  { id:7,  type:"minigame", label:"Mini-Game",  icon:"🎮", color:THEME.blue,   x:540, y:320 },
  { id:8,  type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:480, y:255 },
  { id:9,  type:"bowser",   label:"Bowser!",    icon:"👹", color:THEME.red,    x:410, y:255 },
  { id:10, type:"star",     label:"Star Space", icon:"⭐", color:THEME.red,    x:340, y:255 },
  { id:11, type:"minigame", label:"Mini-Game",  icon:"🎮", color:THEME.blue,   x:270, y:255 },
  { id:12, type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:200, y:255 },
  { id:13, type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:130, y:255 },
  { id:14, type:"star",     label:"Star Space", icon:"⭐", color:THEME.red,    x:60,  y:255 },
  { id:15, type:"minigame", label:"Mini-Game",  icon:"🎮", color:THEME.blue,   x:60,  y:185 },
  { id:16, type:"bowser",   label:"Bowser!",    icon:"👹", color:THEME.red,    x:130, y:185 },
  { id:17, type:"coin",     label:"Coin Bonus", icon:"🪙", color:THEME.yellow, x:200, y:185 },
  { id:18, type:"star",     label:"Star Space", icon:"⭐", color:THEME.red,    x:270, y:120 },
  { id:19, type:"minigame", label:"Mini-Game",  icon:"🎮", color:THEME.blue,   x:340, y:120 },
];

const MINIGAMES = [
  "🏊 Coin Grab Relay — Blooper Bay",
  "🏐 Bowser Dodge Battle",
  "🛶 Canoe & Paddleboard Obstacle",
  "🪢 Boss Battle Tug of War",
  "🏎️ Mario Kart Race on the Lake",
  "🎯 Mushroom Kingdom Trivia",
  "🎵 Musical Mario Chairs",
  "🧩 Star Piece Puzzle Race",
];

const SEED_TRANSACTIONS = [
  { id:1, gid:3, type:"coins", amt:25,  note:"Won Bowser Dodge Battle",     by:"Katelyn", time:"2:34 PM", voided:false },
  { id:2, gid:1, type:"coins", amt:20,  note:"2nd place — Coin Grab Relay", by:"Amanda",  time:"2:35 PM", voided:false },
  { id:3, gid:4, type:"coins", amt:15,  note:"Participation — Coin Grab",   by:"Amanda",  time:"2:36 PM", voided:false },
  { id:4, gid:3, type:"stars", amt:1,   note:"Best group spirit",           by:"Vicki",   time:"3:10 PM", voided:false },
  { id:5, gid:2, type:"coins", amt:-10, note:"Behavior deduction",          by:"Tyler",   time:"3:45 PM", voided:false },
  { id:6, gid:1, type:"stars", amt:1,   note:"Big Stick Award — Jamie S.",  by:"Tyler",   time:"4:00 PM", voided:false },
];

// ─── CSS ─────────────────────────────────────────────────────────────────────
const CSS = `
  @import url('https://fonts.googleapis.com/css2?family=Fredoka+One&family=Nunito:wght@400;600;700;800;900&display=swap');

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg:          ${THEME.bg};
    --black:       ${THEME.black};
    --red:         ${THEME.red};
    --blue:        ${THEME.blue};
    --green:       ${THEME.green};
    --yellow:      ${THEME.yellow};
    --panel-bg:    ${THEME.panelBg};
    --panel-bdr:   ${THEME.panelBorder};
    --shadow:      ${THEME.panelShadow};
    --text-dark:   ${THEME.textDark};
    --text-mid:    ${THEME.textMid};
    --text-light:  ${THEME.textLight};
  }

  body { background: var(--bg); }

  /* ── Dot-grid background texture ── */
  .app {
    min-height: 100vh;
    font-family: 'Nunito', sans-serif;
    color: var(--text-dark);
    background-color: var(--bg);
    background-image: radial-gradient(circle, ${THEME.bgDot} 1.5px, transparent 1.5px);
    background-size: 22px 22px;
    position: relative;
  }

  /* ── Header band ── */
  .site-header {
    background: var(--black);
    border-bottom: 4px solid var(--yellow);
    padding: 12px 18px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    flex-wrap: wrap;
    position: sticky;
    top: 0;
    z-index: 50;
  }

  .logo-lockup { display: flex; align-items: center; gap: 10px; }

  .logo-text {
    font-family: 'Fredoka One', cursive;
    font-size: clamp(16px, 3.5vw, 22px);
    color: var(--yellow);
    letter-spacing: .5px;
    /* outlined text effect */
    -webkit-text-stroke: 0px;
    text-shadow:
      2px 2px 0 #000,
      -1px -1px 0 #000,
       1px -1px 0 #000,
      -1px  1px 0 #000;
  }

  .logo-sub {
    font-size: 9px;
    color: #aaa;
    text-transform: uppercase;
    letter-spacing: 2px;
    font-weight: 700;
    margin-top: 2px;
  }

  /* ── Nav ── */
  .nav {
    display: flex;
    gap: 6px;
    flex-wrap: wrap;
    margin-top: 10px;
    padding: 0 18px 12px;
    background: var(--black);
    border-bottom: 4px solid var(--black);
  }

  .nav-btn {
    font-family: 'Fredoka One', cursive;
    font-size: 13px;
    padding: 7px 16px;
    border-radius: 6px;
    border: 3px solid var(--black);
    cursor: pointer;
    transition: transform .1s, box-shadow .1s;
    color: var(--black);
    background: #ccc;
    box-shadow: 3px 3px 0 var(--black);
    letter-spacing: .3px;
  }
  .nav-btn:hover { transform: translate(-1px,-1px); box-shadow: 4px 4px 0 var(--black); }
  .nav-btn:active { transform: translate(2px,2px); box-shadow: 1px 1px 0 var(--black); }
  .nav-btn.active-red    { background: var(--red);    color: #fff; }
  .nav-btn.active-blue   { background: var(--blue);   color: #fff; }
  .nav-btn.active-green  { background: var(--green);  color: #fff; }
  .nav-btn.active-yellow { background: var(--yellow); color: var(--black); }

  /* ── Panels ── */
  .panel {
    background: var(--panel-bg);
    border: 3px solid var(--black);
    border-radius: 10px;
    box-shadow: var(--shadow);
  }

  .panel-title {
    font-family: 'Fredoka One', cursive;
    font-size: 13px;
    text-transform: uppercase;
    letter-spacing: 1.5px;
    color: var(--text-light);
    margin-bottom: 12px;
  }

  /* ── Buttons ── */
  .btn {
    font-family: 'Fredoka One', cursive;
    font-size: 13px;
    letter-spacing: .4px;
    padding: 9px 18px;
    border-radius: 7px;
    border: 3px solid var(--black);
    cursor: pointer;
    box-shadow: 3px 3px 0 var(--black);
    transition: transform .1s, box-shadow .1s;
    color: var(--black);
  }
  .btn:hover  { transform: translate(-1px,-1px); box-shadow: 4px 4px 0 var(--black); }
  .btn:active { transform: translate(2px,2px);   box-shadow: 1px 1px 0 var(--black); }
  .btn-red    { background: var(--red);    color: #fff; }
  .btn-blue   { background: var(--blue);   color: #fff; }
  .btn-green  { background: var(--green);  color: #fff; }
  .btn-yellow { background: var(--yellow); }
  .btn-white  { background: var(--white);  }

  /* ── Group avatar square ── */
  .avatar {
    width: 38px;
    height: 38px;
    border-radius: 6px;
    border: 3px solid var(--black);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    flex-shrink: 0;
    box-shadow: 2px 2px 0 var(--black);
  }

  /* ── Rank rows ── */
  .rank-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 14px;
    border-bottom: 3px solid var(--black);
    transition: background .15s;
  }
  .rank-row:last-child { border-bottom: none; border-radius: 0 0 7px 7px; }
  .rank-row:first-child { border-radius: 7px 7px 0 0; }
  .rank-row.rank-1 { background: var(--yellow); }
  .rank-row.rank-2 { background: #E8E8E8; }
  .rank-row.rank-3 { background: #F5EED8; }
  .rank-row.rank-other { background: var(--panel-bg); }

  .rank-badge {
    font-family: 'Fredoka One', cursive;
    font-size: 22px;
    width: 36px;
    text-align: center;
    flex-shrink: 0;
    color: var(--black);
  }

  .score-pill {
    display: flex;
    align-items: center;
    gap: 5px;
    background: var(--black);
    border-radius: 5px;
    padding: 4px 10px;
    flex-shrink: 0;
  }
  .score-pill .score-num {
    font-family: 'Fredoka One', cursive;
    font-size: 18px;
    color: #fff;
    /* outlined score number */
    text-shadow: 1px 1px 0 #333;
  }
  .score-pill .score-icon { font-size: 14px; }

  /* ── Currency pill (dashboard cards) ── */
  .currency-block {
    border: 3px solid var(--black);
    border-radius: 7px;
    padding: 8px 12px;
    text-align: center;
    flex: 1;
  }
  .currency-block .c-num {
    font-family: 'Fredoka One', cursive;
    font-size: 28px;
    line-height: 1;
    text-shadow: 2px 2px 0 rgba(0,0,0,.15);
  }
  .currency-block .c-label {
    font-size: 9px;
    font-weight: 800;
    letter-spacing: 1.5px;
    text-transform: uppercase;
    margin-top: 3px;
    color: var(--text-mid);
  }

  /* ── Quick-action btn strip ── */
  .quick-btn {
    font-family: 'Fredoka One', cursive;
    font-size: 12px;
    padding: 6px 10px;
    border-radius: 6px;
    border: 2px solid var(--black);
    cursor: pointer;
    box-shadow: 2px 2px 0 var(--black);
    transition: transform .1s, box-shadow .1s;
    flex: 1;
  }
  .quick-btn:hover  { transform: translate(-1px,-1px); box-shadow: 3px 3px 0 var(--black); }
  .quick-btn:active { transform: translate(1px,1px);   box-shadow: 1px 1px 0 var(--black); }

  /* ── Block ── */
  .number-block {
    width: 110px;
    height: 110px;
    border-radius: 14px;
    border: 5px solid var(--black);
    background: var(--yellow);
    display: flex;
    align-items: center;
    justify-content: center;
    font-family: 'Fredoka One', cursive;
    font-size: 58px;
    color: var(--black);
    cursor: pointer;
    box-shadow: 6px 6px 0 var(--black);
    transition: transform .1s, box-shadow .1s;
    user-select: none;
    text-shadow: 2px 2px 0 rgba(0,0,0,.2);
  }
  .number-block:hover  { transform: translate(-2px,-2px); box-shadow: 8px 8px 0 var(--black); }
  .number-block:active { transform: translate(3px,3px);   box-shadow: 3px 3px 0 var(--black); }
  .number-block.rolling { background: var(--blue); color: #fff; animation: blockPulse .25s ease-in-out infinite alternate; }
  .number-block.reveal  { background: var(--red);  color: #fff; }

  /* ── Modal ── */
  .modal-bg {
    position: fixed; inset: 0;
    background: rgba(26,26,26,.65);
    z-index: 100;
    display: flex; align-items: center; justify-content: center;
    padding: 20px;
  }
  .modal {
    background: var(--panel-bg);
    border: 4px solid var(--black);
    border-radius: 14px;
    padding: 24px;
    width: 100%;
    max-width: 420px;
    box-shadow: 8px 8px 0 var(--black);
    animation: popIn .25s ease;
  }
  .modal-title {
    font-family: 'Fredoka One', cursive;
    font-size: 22px;
    color: var(--black);
    margin-bottom: 18px;
  }

  /* ── Form fields ── */
  .field-label {
    display: block;
    font-size: 10px;
    font-weight: 800;
    letter-spacing: 1.5px;
    text-transform: uppercase;
    color: var(--text-light);
    margin-bottom: 5px;
  }
  input, select, textarea {
    background: var(--bg);
    border: 3px solid var(--black);
    border-radius: 7px;
    color: var(--text-dark);
    padding: 9px 12px;
    font-size: 14px;
    font-family: 'Nunito', sans-serif;
    font-weight: 700;
    width: 100%;
    outline: none;
    transition: border-color .15s, box-shadow .15s;
    box-shadow: 2px 2px 0 var(--black);
  }
  input:focus, select:focus {
    border-color: var(--blue);
    box-shadow: 2px 2px 0 var(--blue);
  }
  select option { background: var(--bg); color: var(--text-dark); }

  /* ── Toast ── */
  .toast {
    position: fixed;
    bottom: 24px;
    left: 50%;
    transform: translateX(-50%);
    border: 3px solid var(--black);
    border-radius: 9px;
    padding: 10px 20px;
    font-family: 'Fredoka One', cursive;
    font-size: 15px;
    z-index: 200;
    box-shadow: 4px 4px 0 var(--black);
    animation: popIn .25s ease;
    white-space: nowrap;
  }

  /* ── Tx log row ── */
  .tx-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 14px;
    border-bottom: 2px solid #E8E0CC;
  }
  .tx-row:last-child { border-bottom: none; }
  .tx-icon {
    width: 36px;
    height: 36px;
    border-radius: 6px;
    border: 2px solid var(--black);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 16px;
    flex-shrink: 0;
    box-shadow: 2px 2px 0 var(--black);
  }

  /* ── Board space ── */
  .space-label {
    font-family: 'Fredoka One', cursive;
    font-size: 9px;
  }

  /* ── Projector leaderboard ── */
  .projector-overlay {
    position: fixed; inset: 0;
    background: var(--bg);
    background-image: radial-gradient(circle, ${THEME.bgDot} 1.5px, transparent 1.5px);
    background-size: 28px 28px;
    z-index: 300;
    display: flex;
    flex-direction: column;
    padding: 32px 40px;
    gap: 20px;
  }
  .proj-header {
    text-align: center;
    font-family: 'Fredoka One', cursive;
    font-size: clamp(28px, 5vw, 52px);
    color: var(--black);
    letter-spacing: 1px;
    text-shadow: 4px 4px 0 var(--yellow);
    border-bottom: 5px solid var(--black);
    padding-bottom: 16px;
  }
  .proj-row {
    display: flex;
    align-items: center;
    gap: 24px;
    padding: 20px 28px;
    border: 5px solid var(--black);
    border-radius: 14px;
    box-shadow: 8px 8px 0 var(--black);
    flex: 1;
  }
  .proj-row.proj-rank-1 { background: var(--yellow); }
  .proj-row.proj-rank-2 { background: #DDEEFF; border-color: var(--blue); box-shadow: 8px 8px 0 var(--blue); }
  .proj-row.proj-rank-3 { background: #DDFCE8; border-color: var(--green); box-shadow: 8px 8px 0 var(--green); }
  .proj-row.proj-rank-other { background: var(--panel-bg); }
  .proj-rank-num {
    font-family: 'Fredoka One', cursive;
    font-size: clamp(36px, 6vw, 64px);
    color: var(--black);
    width: 80px;
    text-align: center;
    flex-shrink: 0;
  }
  .proj-avatar {
    width: 72px; height: 72px;
    border-radius: 10px;
    border: 5px solid var(--black);
    display: flex; align-items: center; justify-content: center;
    font-size: 38px;
    flex-shrink: 0;
    box-shadow: 4px 4px 0 var(--black);
  }
  .proj-name {
    font-family: 'Fredoka One', cursive;
    font-size: clamp(22px, 3.5vw, 40px);
    flex: 1;
    color: var(--black);
  }
  .proj-score {
    display: flex;
    align-items: center;
    gap: 16px;
    flex-shrink: 0;
  }
  .proj-score-item {
    display: flex;
    align-items: center;
    gap: 8px;
    background: var(--black);
    border-radius: 10px;
    padding: 10px 20px;
    border: 3px solid var(--black);
  }
  .proj-score-num {
    font-family: 'Fredoka One', cursive;
    font-size: clamp(28px, 4vw, 48px);
    color: #fff;
    text-shadow: 2px 2px 0 #333;
  }
  .proj-score-icon { font-size: clamp(20px, 3vw, 32px); }

  /* ── Mini-game spinner ── */
  .mg-card {
    background: var(--panel-bg);
    border: 3px solid var(--black);
    border-radius: 10px;
    box-shadow: var(--shadow);
    padding: 32px 24px;
    text-align: center;
    max-width: 500px;
    margin: 0 auto;
    width: 100%;
  }
  .mg-display {
    font-family: 'Fredoka One', cursive;
    font-size: clamp(18px, 4vw, 26px);
    color: var(--black);
    min-height: 52px;
    display: flex;
    align-items: center;
    justify-content: center;
    border: 3px solid var(--black);
    border-radius: 8px;
    padding: 12px 16px;
    background: var(--bg);
    margin: 16px 0;
    box-shadow: inset 2px 2px 0 rgba(0,0,0,.08);
  }

  /* ── Animations ── */
  @keyframes popIn {
    0%   { transform: scale(.8); opacity: 0; }
    70%  { transform: scale(1.04); }
    100% { transform: scale(1); opacity: 1; }
  }
  @keyframes slideUp {
    from { transform: translateY(14px); opacity: 0; }
    to   { transform: translateY(0);    opacity: 1; }
  }
  @keyframes blockPulse {
    from { box-shadow: 6px 6px 0 var(--black); }
    to   { box-shadow: 8px 8px 0 var(--black), 0 0 0 4px var(--yellow); }
  }
  @keyframes bounce {
    0%,100% { transform: translateY(0); }
    50%     { transform: translateY(-6px); }
  }
  @keyframes handWave {
    0%,100% { transform: rotate(-15deg) scaleX(-1); }
    50%     { transform: rotate(15deg)  scaleX(-1); }
  }
  @keyframes shimmer {
    0%   { background-position: -200% center; }
    100% { background-position:  200% center; }
  }
`;

// ─── APP ─────────────────────────────────────────────────────────────────────
export default function App() {
  const [groups, setGroups]     = useState(GROUPS.map(g => ({
    ...g,
    coins: [142,98,167,115][g.id-1],
    stars: [3,2,4,2][g.id-1],
  })));
  const [txs, setTxs]           = useState(SEED_TRANSACTIONS);
  const [view, setView]         = useState("Dashboard");
  const [projector, setProjector] = useState(false);

  // Tx modal
  const [txModal, setTxModal]   = useState(false);
  const [txForm, setTxForm]     = useState({ gid:"", type:"coins", amt:"", note:"" });

  // Toast
  const [toast, setToast]       = useState(null);

  // Block hit
  const [blockModal, setBlockModal]       = useState(false);
  const [blockGroup, setBlockGroup]       = useState(null);
  const [blockPhase, setBlockPhase]       = useState("idle");
  const [blockNumber, setBlockNumber]     = useState(null);
  const [blockDisplay, setBlockDisplay]   = useState(null);

  // Mini-game
  const [mgPhase, setMgPhase]   = useState("idle");
  const [mgIndex, setMgIndex]   = useState(0);
  const [mgResult, setMgResult] = useState(null);

  const nextId  = useRef(7);
  const spinRef = useRef(null);

  const showToast = (msg, bg=THEME.green) => {
    setToast({msg,bg}); setTimeout(()=>setToast(null),2800);
  };

  // ── Transactions ──
  const submitTx = () => {
    const amt = parseInt(txForm.amt);
    if (!txForm.gid || isNaN(amt)) return;
    const tx = {
      id: nextId.current++,
      gid: parseInt(txForm.gid),
      type: txForm.type, amt,
      note: txForm.note || "—",
      by: "Tyler",
      time: new Date().toLocaleTimeString([],{hour:"2-digit",minute:"2-digit"}),
      voided: false,
    };
    setTxs(p => [tx, ...p]);
    setGroups(p => p.map(g => g.id !== tx.gid ? g :
      tx.type === "coins"
        ? {...g, coins: Math.max(0, g.coins + amt)}
        : {...g, stars: Math.max(0, g.stars + amt)}
    ));
    setTxModal(false);
    setTxForm({gid:"",type:"coins",amt:"",note:""});
    showToast("✅ Transaction logged!");
  };

  const voidTx = (id) => {
    const tx = txs.find(t=>t.id===id);
    if (!tx || tx.voided) return;
    setTxs(p => p.map(t => t.id===id ? {...t,voided:true} : t));
    setGroups(p => p.map(g => g.id!==tx.gid ? g :
      tx.type==="coins"
        ? {...g, coins: Math.max(0,g.coins-tx.amt)}
        : {...g, stars: Math.max(0,g.stars-tx.amt)}
    ));
    showToast("↩️ Voided", THEME.red);
  };

  // ── Block hit ──
  const startBlockHit = (gid) => {
    setBlockPhase("rolling"); setBlockNumber(null); setBlockDisplay(null);
    const dest  = SCRIPTED_DESTINATIONS[gid];
    const g     = groups.find(x=>x.id===gid);
    const steps = ((dest - g.boardPos) + BOARD_SPACES.length) % BOARD_SPACES.length || BOARD_SPACES.length;
    const dur   = 2200, start = Date.now();
    const roll  = () => {
      const el = Date.now()-start, prog = el/dur;
      setBlockDisplay(Math.floor(Math.random()*12)+1);
      if (el < dur) {
        spinRef.current = setTimeout(roll, Math.max(40, prog*350));
      } else {
        setBlockDisplay(steps); setBlockNumber(steps); setBlockPhase("reveal");
        setTimeout(() => {
          setBlockPhase("moving");
          setGroups(p => p.map(g => g.id!==gid ? g : {...g, boardPos: dest}));
          setTimeout(() => setBlockPhase("done"), 1200);
        }, 1400);
      }
    };
    spinRef.current = setTimeout(roll, 80);
  };

  // ── Mini-game ──
  const startMgSpin = () => {
    setMgPhase("spinning"); setMgResult(null);
    let i=0; const dur=3000, start=Date.now();
    const spin = () => {
      const el=Date.now()-start, prog=el/dur;
      setMgIndex(i%MINIGAMES.length); i++;
      if (el < dur) {
        spinRef.current = setTimeout(spin, Math.max(50, prog*300));
      } else {
        setMgResult(MINIGAMES[i%MINIGAMES.length]); setMgPhase("result");
      }
    };
    spinRef.current = setTimeout(spin, 80);
  };

  useEffect(() => () => clearTimeout(spinRef.current), []);

  const sorted = [...groups].sort((a,b) => (b.stars*10000+b.coins)-(a.stars*10000+a.coins));

  const NAV = [
    { id:"Dashboard", icon:"📊", active:"active-red"    },
    { id:"Board",     icon:"🗺️", active:"active-blue"   },
    { id:"Mini-Games",icon:"🎮", active:"active-green"  },
    { id:"Transactions",icon:"📋",active:"active-yellow"},
  ];

  // ─── RENDER ───────────────────────────────────────────────────────────────
  return (
    <div className="app">
      <style>{CSS}</style>

      {/* ── Projector leaderboard overlay ── */}
      {projector && (
        <div className="projector-overlay">
          <div style={{display:"flex",justifyContent:"space-between",alignItems:"flex-start"}}>
            <div className="proj-header" style={{flex:1}}>
              🏆 SUPER CLOT NOT PARTY '26 — STANDINGS
            </div>
            <button
              className="btn btn-white"
              style={{marginLeft:16,flexShrink:0,fontSize:12}}
              onClick={()=>setProjector(false)}
            >✕ Exit</button>
          </div>

          {sorted.map((g,rank) => {
            const cls = rank===0?"proj-rank-1":rank===1?"proj-rank-2":rank===2?"proj-rank-3":"proj-rank-other";
            const medals = ["🥇","🥈","🥉","4️⃣"];
            return (
              <div key={g.id} className={`proj-row ${cls}`} style={{animation:`slideUp .3s ease ${rank*.08}s both`}}>
                <div className="proj-rank-num">{medals[rank]}</div>
                <div className="proj-avatar" style={{background:g.color}}>{g.avatar}</div>
                <div className="proj-name">{g.name}</div>
                <div className="proj-score">
                  <div className="proj-score-item" style={{background:THEME.yellow,border:`3px solid ${THEME.black}`}}>
                    <span className="proj-score-icon">🪙</span>
                    <span className="proj-score-num" style={{color:THEME.black}}>{g.coins}</span>
                  </div>
                  <div className="proj-score-item">
                    <span className="proj-score-icon">⭐</span>
                    <span className="proj-score-num">{"⭐".repeat(Math.min(g.stars,5))} <span style={{fontSize:"0.6em",opacity:.8}}>({g.stars})</span></span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ── Site header ── */}
      <div className="site-header">
        <div className="logo-lockup">
          <div style={{
            width:44, height:44, borderRadius:8,
            border:`3px solid ${THEME.yellow}`,
            display:"flex",alignItems:"center",justifyContent:"center",
            fontSize:24, background:THEME.black,
            flexShrink:0,
          }}>🏁</div>
          <div>
            <div className="logo-text">SUPER CLOT NOT PARTY '26</div>
            <div className="logo-sub">Camp Clot Not · Staff Dashboard</div>
          </div>
        </div>
        <div style={{display:"flex",gap:8,flexWrap:"wrap"}}>
          <button className="btn btn-yellow" style={{fontSize:12,padding:"7px 14px"}} onClick={()=>setProjector(true)}>
            📺 Projector
          </button>
          <button className="btn btn-red" style={{fontSize:12,padding:"7px 14px"}} onClick={()=>setTxModal(true)}>
            ＋ Log Transaction
          </button>
        </div>
      </div>

      {/* ── Nav ── */}
      <div className="nav">
        {NAV.map(n => (
          <button
            key={n.id}
            className={`nav-btn ${view===n.id ? n.active : ""}`}
            onClick={() => setView(n.id)}
          >
            {n.icon} {n.id}
          </button>
        ))}
      </div>

      {/* ── Content ── */}
      <div style={{padding:"20px 18px 60px"}}>

        {/* ══ DASHBOARD ══ */}
        {view==="Dashboard" && (
          <div style={{animation:"slideUp .3s ease"}}>

            {/* Standings ranked rows */}
            <div className="panel-title">🏆 Standings</div>
            <div className="panel" style={{marginBottom:24,overflow:"hidden"}}>
              {sorted.map((g,rank) => {
                const cls = rank===0?"rank-1":rank===1?"rank-2":rank===2?"rank-3":"rank-other";
                const medals = ["🥇","🥈","🥉","4️⃣"];
                return (
                  <div key={g.id} className={`rank-row rank-${cls}`}>
                    <div className="rank-badge">{medals[rank]}</div>
                    {/* Square avatar */}
                    <div className="avatar" style={{background:g.color}}>
                      {g.avatar}
                    </div>
                    {/* Name + position */}
                    <div style={{flex:1,minWidth:0}}>
                      <div style={{fontFamily:"'Fredoka One',cursive",fontSize:15,color:THEME.black,whiteSpace:"nowrap",overflow:"hidden",textOverflow:"ellipsis"}}>{g.name}</div>
                      <div style={{fontSize:10,color:THEME.textLight,fontWeight:700,marginTop:1}}>
                        Space {g.boardPos} · {BOARD_SPACES[g.boardPos]?.icon} {BOARD_SPACES[g.boardPos]?.label}
                      </div>
                    </div>
                    {/* Score pills */}
                    <div style={{display:"flex",gap:6,flexShrink:0}}>
                      <div className="score-pill" style={{background:THEME.yellow}}>
                        <span className="score-icon">🪙</span>
                        <span className="score-num" style={{color:THEME.black}}>{g.coins}</span>
                      </div>
                      <div className="score-pill">
                        <span className="score-icon">⭐</span>
                        <span className="score-num">{g.stars}</span>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Expanded group cards with quick actions */}
            <div className="panel-title">⚡ Quick Actions</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(260px,1fr))",gap:14,marginBottom:24}}>
              {sorted.map((g,rank) => (
                <div key={g.id} className="panel" style={{
                  padding:14,
                  borderTopWidth:5,
                  borderTopColor:g.color,
                  animation:`slideUp .3s ease ${rank*.06}s both`,
                }}>
                  <div style={{display:"flex",alignItems:"center",gap:10,marginBottom:12}}>
                    <div className="avatar" style={{background:g.color}}>{g.avatar}</div>
                    <div>
                      <div style={{fontFamily:"'Fredoka One',cursive",fontSize:14}}>{g.name}</div>
                      <div style={{fontSize:10,color:THEME.textLight,fontWeight:700,marginTop:1}}>
                        {BOARD_SPACES[g.boardPos]?.icon} Space {g.boardPos}
                      </div>
                    </div>
                  </div>
                  <div style={{display:"flex",gap:10,marginBottom:12}}>
                    <div className="currency-block" style={{borderTopColor:THEME.yellow,borderTopWidth:4}}>
                      <div className="c-num" style={{color:THEME.black}}>{g.coins}</div>
                      <div className="c-label">🪙 Coins</div>
                    </div>
                    <div className="currency-block" style={{borderTopColor:THEME.red,borderTopWidth:4}}>
                      <div className="c-num" style={{color:THEME.black,fontSize:22}}>{"⭐".repeat(Math.min(g.stars,5))}</div>
                      <div className="c-label">Stars ({g.stars})</div>
                    </div>
                  </div>
                  <div style={{display:"flex",gap:8}}>
                    <button className="quick-btn" style={{background:THEME.yellow}} onClick={()=>{setTxModal(true);setTxForm({gid:String(g.id),type:"coins",amt:"",note:""})}}>+ Coins</button>
                    <button className="quick-btn" style={{background:THEME.red,color:"#fff"}}  onClick={()=>{setTxModal(true);setTxForm({gid:String(g.id),type:"stars",amt:"",note:""})}}>+ Stars</button>
                  </div>
                </div>
              ))}
            </div>

            {/* Recent activity */}
            <div className="panel-title">🕐 Recent Activity</div>
            <div className="panel" style={{overflow:"hidden"}}>
              {txs.filter(t=>!t.voided).slice(0,5).map((tx,i) => {
                const g = groups.find(x=>x.id===tx.gid);
                return (
                  <div key={tx.id} className="tx-row">
                    <div className="tx-icon" style={{background:tx.type==="coins"?THEME.yellow:THEME.red}}>
                      {tx.type==="coins"?"🪙":"⭐"}
                    </div>
                    <div style={{flex:1}}>
                      <span style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:g?.color}}>{g?.name}</span>
                      <span style={{
                        marginLeft:8,fontWeight:800,fontSize:13,
                        color:tx.amt>0?THEME.green:THEME.red
                      }}>{tx.amt>0?"+":""}{tx.amt} {tx.type}</span>
                      <div style={{fontSize:10,color:THEME.textLight,marginTop:2}}>{tx.note} · {tx.by} · {tx.time}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* ══ BOARD ══ */}
        {view==="Board" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:14}}>
              <div className="panel-title" style={{margin:0}}>🗺️ Game Board</div>
              <button className="btn btn-yellow" style={{fontSize:12,padding:"7px 14px"}}
                onClick={()=>{setBlockModal(true);setBlockPhase("idle");setBlockNumber(null);setBlockDisplay(null);setBlockGroup(null);}}>
                ⁉️ Block Hit
              </button>
            </div>

            <div className="panel" style={{padding:12,marginBottom:18,overflow:"hidden"}}>
              <svg viewBox="0 0 600 430" style={{width:"100%",height:"auto",display:"block"}}>
                {/* Board path */}
                <polyline
                  points="60,385 130,385 200,385 270,385 340,385 410,385 480,385 540,320 480,255 410,255 340,255 270,255 200,255 130,255 60,255 60,185 130,185 200,185 270,120 340,120"
                  stroke={THEME.black} strokeWidth={3} strokeDasharray="8 5" fill="none" opacity={.3}
                />

                {BOARD_SPACES.map(s => {
                  const groupsHere = groups.filter(g=>g.boardPos===s.id);
                  return (
                    <g key={s.id}>
                      {/* Space tile */}
                      <rect x={s.x-17} y={s.y-17} width={34} height={34} rx={6}
                        fill={s.color} stroke={THEME.black} strokeWidth={3}
                      />
                      <text x={s.x} y={s.y+1} textAnchor="middle" dominantBaseline="middle" fontSize={14}>{s.icon}</text>
                      <text x={s.x} y={s.y+24} textAnchor="middle" fontSize={7} fill="rgba(0,0,0,.4)" fontFamily="Fredoka One,cursive">{s.id}</text>
                      {/* Tokens */}
                      {groupsHere.map((g,gi) => {
                        const offX = groupsHere.length>1?(gi-.5)*16:0;
                        return (
                          <g key={g.id}>
                            <rect x={s.x+offX-9} y={s.y-36} width={18} height={18} rx={3}
                              fill={g.color} stroke={THEME.black} strokeWidth={2}
                            />
                            <text x={s.x+offX} y={s.y-27} textAnchor="middle" dominantBaseline="middle" fontSize={10}>{g.avatar}</text>
                          </g>
                        );
                      })}
                    </g>
                  );
                })}
                <text x="60" y="408" textAnchor="middle" fontSize={8} fontWeight="700" fill={THEME.black} fontFamily="Fredoka One,cursive">START</text>
              </svg>
            </div>

            {/* Legend */}
            <div style={{display:"flex",gap:8,flexWrap:"wrap",marginBottom:18}}>
              {[
                {c:THEME.blue,  i:"🎮",l:"Mini-Game"},
                {c:THEME.yellow,i:"🪙",l:"Coin"},
                {c:THEME.red,   i:"⭐",l:"Star Space"},
                {c:THEME.red,   i:"👹",l:"Bowser!"},
              ].map(x=>(
                <div key={x.l} style={{
                  display:"flex",alignItems:"center",gap:6,
                  background:THEME.panelBg,border:`2px solid ${THEME.black}`,
                  borderRadius:6,padding:"4px 10px",fontSize:11,
                  fontWeight:700,boxShadow:"2px 2px 0 #1A1A1A",
                }}>
                  <div style={{width:8,height:8,borderRadius:2,background:x.c,border:`1px solid ${THEME.black}`}}/>
                  {x.i} {x.l}
                </div>
              ))}
            </div>

            {/* Group position list */}
            <div className="panel-title">Group Positions</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(220px,1fr))",gap:10}}>
              {groups.map(g => {
                const sp = BOARD_SPACES[g.boardPos];
                return (
                  <div key={g.id} className="panel" style={{padding:"12px 14px",display:"flex",alignItems:"center",gap:10,borderTopColor:g.color,borderTopWidth:4}}>
                    <div className="avatar" style={{background:g.color,width:32,height:32,fontSize:15}}>{g.avatar}</div>
                    <div style={{flex:1}}>
                      <div style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:g.color}}>{g.short}</div>
                      <div style={{fontSize:10,color:THEME.textLight,marginTop:2}}>{sp?.icon} Space {g.boardPos}: {sp?.label}</div>
                    </div>
                    <div style={{fontSize:11,fontFamily:"'Fredoka One',cursive",color:THEME.black}}>🪙{g.coins} ⭐{g.stars}</div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* ══ MINI-GAMES ══ */}
        {view==="Mini-Games" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div className="panel-title" style={{textAlign:"center"}}>🎮 Evening Challenge Picker</div>
            <div style={{textAlign:"center",fontSize:12,color:THEME.textLight,marginBottom:24,fontWeight:700}}>
              Minute to Win It &amp; group challenges — the big nightly events.
            </div>

            <div className="mg-card" style={{marginBottom:24}}>
              {mgPhase==="idle" && (
                <>
                  <div style={{fontSize:56,marginBottom:8,animation:"bounce 2s ease-in-out infinite"}}>🎲</div>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:18,color:THEME.black}}>Ready to spin?</div>
                  <div style={{fontSize:12,color:THEME.textLight,marginTop:4,fontWeight:700}}>Tap the button below to reveal tonight's challenge</div>
                  <div className="mg-display" style={{opacity:.4,fontSize:14}}>—</div>
                </>
              )}
              {mgPhase==="spinning" && (
                <>
                  <div style={{fontSize:52,display:"inline-block",animation:"handWave 1s ease-in-out infinite",marginBottom:8}}>🫳</div>
                  <div className="mg-display">{MINIGAMES[mgIndex]}</div>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:THEME.blue,letterSpacing:1}}>SPINNING...</div>
                </>
              )}
              {mgPhase==="result" && (
                <div style={{animation:"popIn .35s ease"}}>
                  <div style={{fontSize:48,marginBottom:8}}>🎉</div>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:12,color:THEME.green,letterSpacing:2,textTransform:"uppercase",marginBottom:8}}>Tonight's Challenge!</div>
                  <div className="mg-display" style={{background:THEME.yellow,fontSize:"clamp(16px,3.5vw,22px)"}}>{mgResult}</div>
                </div>
              )}

              <div style={{display:"flex",gap:10,justifyContent:"center",marginTop:16}}>
                {mgPhase!=="spinning" && (
                  <button className="btn btn-blue" onClick={startMgSpin}>
                    {mgPhase==="result"?"🔄 Spin Again":"🎲 Spin!"}
                  </button>
                )}
                {mgPhase==="result" && (
                  <button className="btn btn-white" onClick={()=>{setMgPhase("idle");setMgResult(null);}}>Reset</button>
                )}
              </div>
            </div>

            <div className="panel-title">All Challenges</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(210px,1fr))",gap:10}}>
              {MINIGAMES.map((mg,i)=>(
                <div key={i} className="panel" style={{padding:"12px 14px",display:"flex",alignItems:"center",gap:10}}>
                  <div className="avatar" style={{background:THEME.blue,color:"#fff",width:32,height:32,fontSize:15}}>🎮</div>
                  <div style={{fontSize:13,fontWeight:700,lineHeight:1.3,color:THEME.textDark}}>{mg}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ══ TRANSACTIONS ══ */}
        {view==="Transactions" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:16}}>
              <div className="panel-title" style={{margin:0}}>📋 Transaction Log</div>
              <button className="btn btn-red" style={{fontSize:12,padding:"7px 14px"}} onClick={()=>setTxModal(true)}>＋ New</button>
            </div>
            <div className="panel" style={{overflow:"hidden"}}>
              {txs.map((tx,i) => {
                const g = groups.find(x=>x.id===tx.gid);
                return (
                  <div key={tx.id} className="tx-row" style={{opacity:tx.voided?.45:1}}>
                    <div className="tx-icon" style={{background:tx.type==="coins"?THEME.yellow:THEME.red}}>
                      {tx.type==="coins"?"🪙":"⭐"}
                    </div>
                    <div style={{flex:1,minWidth:0}}>
                      <div style={{display:"flex",alignItems:"center",gap:6,flexWrap:"wrap"}}>
                        <span style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:g?.color}}>{g?.name}</span>
                        <span style={{fontWeight:800,fontSize:13,color:tx.amt>0?THEME.green:THEME.red}}>
                          {tx.amt>0?"+":""}{tx.amt} {tx.type}
                        </span>
                        {tx.voided && (
                          <span style={{
                            fontSize:9,fontWeight:800,color:THEME.red,
                            background:"#FFE5E5",border:`2px solid ${THEME.red}`,
                            borderRadius:4,padding:"1px 6px",letterSpacing:.5,
                          }}>VOIDED</span>
                        )}
                      </div>
                      <div style={{fontSize:10,color:THEME.textLight,marginTop:2,fontWeight:700}}>{tx.note} · {tx.by} · {tx.time}</div>
                    </div>
                    {!tx.voided && (
                      <button
                        onClick={()=>voidTx(tx.id)}
                        style={{
                          background:"#FFE5E5",border:`2px solid ${THEME.red}`,borderRadius:6,
                          color:THEME.red,padding:"5px 10px",cursor:"pointer",
                          fontSize:11,fontWeight:800,flexShrink:0,
                          fontFamily:"Nunito,sans-serif",
                          boxShadow:`2px 2px 0 ${THEME.red}`,
                        }}
                      >Void</button>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* ══ BLOCK HIT MODAL ══ */}
      {blockModal && (
        <div className="modal-bg" onClick={e=>e.target===e.currentTarget&&blockPhase==="idle"&&setBlockModal(false)}>
          <div className="modal">
            <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:18}}>
              <div className="modal-title">⁉️ Block Hit!</div>
              {blockPhase==="idle"&&<button className="btn btn-white" style={{fontSize:12,padding:"5px 12px"}} onClick={()=>setBlockModal(false)}>✕</button>}
            </div>

            {(blockPhase==="idle"||blockPhase==="done") && (
              <div style={{marginBottom:20}}>
                <label className="field-label">Which group is hitting?</label>
                <div style={{display:"flex",gap:8,flexWrap:"wrap",marginTop:6}}>
                  {groups.map(g=>(
                    <button key={g.id} onClick={()=>{setBlockGroup(g.id);setBlockPhase("idle");setBlockDisplay(null);}} style={{
                      background:blockGroup===g.id?g.color:THEME.panelBg,
                      border:`3px solid ${blockGroup===g.id?g.color:THEME.black}`,
                      borderRadius:8, color:blockGroup===g.id?"#fff":THEME.black,
                      padding:"8px 14px", cursor:"pointer", fontWeight:800, fontSize:13,
                      fontFamily:"'Fredoka One',cursive",
                      boxShadow:blockGroup===g.id?`3px 3px 0 ${THEME.black}`:`2px 2px 0 ${THEME.black}`,
                      transition:"all .15s",
                    }}>{g.avatar} {g.short}</button>
                  ))}
                </div>
              </div>
            )}

            <div style={{display:"flex",flexDirection:"column",alignItems:"center",gap:12}}>
              <div
                className={`number-block ${blockPhase==="rolling"?"rolling":blockPhase==="reveal"?"reveal":"" }`}
                onClick={()=>blockGroup&&blockPhase==="idle"&&startBlockHit(blockGroup)}
                style={{cursor:blockGroup&&blockPhase==="idle"?"pointer":"default"}}
              >
                {blockDisplay ?? "?"}
              </div>

              {blockPhase==="idle"&&!blockGroup&&<div style={{fontSize:12,color:THEME.textLight,fontWeight:700}}>Select a group to begin</div>}
              {blockPhase==="idle"&&blockGroup&&<div style={{fontFamily:"'Fredoka One',cursive",fontSize:14,color:THEME.blue}}>Tap the block!</div>}
              {blockPhase==="rolling"&&<div style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:THEME.blue,letterSpacing:1}}>ROLLING...</div>}

              {blockPhase==="reveal"&&blockNumber&&(
                <div style={{textAlign:"center",animation:"popIn .4s ease"}}>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:18,color:THEME.black}}>
                    Moves <span style={{color:THEME.red,fontSize:28}}>{blockNumber}</span> spaces!
                  </div>
                  <div style={{fontSize:11,color:THEME.textLight,fontWeight:700,marginTop:4}}>Moving token to space {SCRIPTED_DESTINATIONS[blockGroup]}...</div>
                </div>
              )}

              {blockPhase==="moving"&&(
                <div style={{textAlign:"center"}}>
                  <div style={{fontSize:28,animation:"bounce .5s ease-in-out infinite"}}>🏃</div>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:13,color:THEME.blue,marginTop:4}}>Moving...</div>
                </div>
              )}

              {blockPhase==="done"&&(()=>{
                const g  = groups.find(x=>x.id===blockGroup);
                const sp = BOARD_SPACES[g?.boardPos];
                return (
                  <div style={{textAlign:"center",animation:"popIn .4s ease"}}>
                    <div style={{fontSize:38,marginBottom:6}}>{sp?.icon}</div>
                    <div style={{fontFamily:"'Fredoka One',cursive",fontSize:18,color:THEME.green}}>Landed on {sp?.label}!</div>
                    <div style={{fontSize:11,color:THEME.textLight,fontWeight:700,marginTop:4}}>Space {g?.boardPos} · {g?.name}</div>
                    <div style={{display:"flex",gap:8,marginTop:16,justifyContent:"center"}}>
                      <button className="btn btn-white" style={{fontSize:12}} onClick={()=>{setBlockPhase("idle");setBlockDisplay(null);setBlockNumber(null);}}>Another Group</button>
                      <button className="btn btn-green" style={{fontSize:12}} onClick={()=>{setBlockModal(false);setBlockPhase("idle");setBlockDisplay(null);setBlockNumber(null);setView("Board");}}>View Board</button>
                    </div>
                  </div>
                );
              })()}
            </div>
          </div>
        </div>
      )}

      {/* ══ TRANSACTION MODAL ══ */}
      {txModal && (
        <div className="modal-bg" onClick={e=>e.target===e.currentTarget&&setTxModal(false)}>
          <div className="modal">
            <div className="modal-title">Log Transaction</div>
            <div style={{display:"flex",flexDirection:"column",gap:14}}>
              <div>
                <label className="field-label">Group</label>
                <select value={txForm.gid} onChange={e=>setTxForm(f=>({...f,gid:e.target.value}))}>
                  <option value="">Select a group...</option>
                  {groups.map(g=><option key={g.id} value={g.id}>{g.avatar} {g.name}</option>)}
                </select>
              </div>
              <div>
                <label className="field-label">Type</label>
                <select value={txForm.type} onChange={e=>setTxForm(f=>({...f,type:e.target.value}))}>
                  <option value="coins">🪙 Coins</option>
                  <option value="stars">⭐ Stars</option>
                </select>
              </div>
              <div>
                <label className="field-label">Amount (negative to deduct)</label>
                <input type="number" placeholder="e.g. 25 or -10" value={txForm.amt} onChange={e=>setTxForm(f=>({...f,amt:e.target.value}))} />
              </div>
              <div>
                <label className="field-label">Note (optional)</label>
                <input type="text" placeholder="e.g. Won Bowser Dodge Battle" value={txForm.note} onChange={e=>setTxForm(f=>({...f,note:e.target.value}))} />
              </div>
              <div style={{display:"flex",gap:10,marginTop:4}}>
                <button className="btn btn-white" style={{flex:1}} onClick={()=>setTxModal(false)}>Cancel</button>
                <button className="btn btn-red"   style={{flex:2}} onClick={submitTx}>Log It</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Toast */}
      {toast && (
        <div className="toast" style={{background:toast.bg,color:toast.bg===THEME.yellow?THEME.black:"#fff"}}>
          {toast.msg}
        </div>
      )}
    </div>
  );
}
