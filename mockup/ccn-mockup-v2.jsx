import { useState, useEffect, useRef, useCallback } from "react";

// ─── DATA ───────────────────────────────────────────────────────────────────

const GROUPS = [
  { id: 1, name: "Mario's Mushroom Crew", short: "Mushroom", char: "🔴", color: "#E84040", bg: "#FDEDEC", boardPos: 3 },
  { id: 2, name: "Luigi's Green Machine", short: "Green",    char: "🟢", color: "#2ECC71", bg: "#EAFAF1", boardPos: 6 },
  { id: 3, name: "Peach's Power Squad",   short: "Peach",    char: "🌸", color: "#E91E8C", bg: "#FDE8F4", boardPos: 10 },
  { id: 4, name: "Toad's Toadstool Tribe",short: "Toadstool",char: "🔵", color: "#3498DB", bg: "#EBF5FB", boardPos: 5 },
];

// Pre-scripted block hit destinations (where each group will land on their turn)
const SCRIPTED_DESTINATIONS = { 1: 7, 2: 11, 3: 2, 4: 9 };

// Winding rectangular board path — 20 spaces in a loose snake shape
// Each entry: { id, type, label, icon, color, x, y } (x/y in 0-100% of SVG viewBox 600x420)
const BOARD_SPACES = [
  { id:0,  type:"start",    label:"START",         icon:"🏁", color:"#F39C12", x:60,  y:385 },
  { id:1,  type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:130, y:385 },
  { id:2,  type:"minigame", label:"Mini-Game",     icon:"🎮", color:"#9B59B6", x:200, y:385 },
  { id:3,  type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:270, y:385 },
  { id:4,  type:"bowser",   label:"Bowser!",       icon:"👹", color:"#E74C3C", x:340, y:385 },
  { id:5,  type:"star",     label:"Star Space",    icon:"⭐", color:"#E74C3C", x:410, y:385 },
  { id:6,  type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:480, y:385 },
  { id:7,  type:"minigame", label:"Mini-Game",     icon:"🎮", color:"#9B59B6", x:540, y:320 },
  { id:8,  type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:480, y:255 },
  { id:9,  type:"bowser",   label:"Bowser!",       icon:"👹", color:"#E74C3C", x:410, y:255 },
  { id:10, type:"star",     label:"Star Space",    icon:"⭐", color:"#E74C3C", x:340, y:255 },
  { id:11, type:"minigame", label:"Mini-Game",     icon:"🎮", color:"#9B59B6", x:270, y:255 },
  { id:12, type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:200, y:255 },
  { id:13, type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:130, y:255 },
  { id:14, type:"star",     label:"Star Space",    icon:"⭐", color:"#E74C3C", x:60,  y:255 },
  { id:15, type:"minigame", label:"Mini-Game",     icon:"🎮", color:"#9B59B6", x:60,  y:185 },
  { id:16, type:"bowser",   label:"Bowser!",       icon:"👹", color:"#E74C3C", x:130, y:185 },
  { id:17, type:"coin",     label:"Coin Bonus",    icon:"🪙", color:"#F39C12", x:200, y:185 },
  { id:18, type:"star",     label:"Star Space",    icon:"⭐", color:"#E74C3C", x:270, y:120 },
  { id:19, type:"minigame", label:"Mini-Game",     icon:"🎮", color:"#9B59B6", x:340, y:120 },
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
  { id:1, gid:3, type:"coins", amt:25,  note:"Won Bowser Dodge Battle",       by:"Katelyn", time:"2:34 PM", voided:false },
  { id:2, gid:1, type:"coins", amt:20,  note:"2nd place — Coin Grab Relay",   by:"Amanda",  time:"2:35 PM", voided:false },
  { id:3, gid:4, type:"coins", amt:15,  note:"Participation — Coin Grab",     by:"Amanda",  time:"2:36 PM", voided:false },
  { id:4, gid:3, type:"stars", amt:1,   note:"Best group spirit",             by:"Vicki",   time:"3:10 PM", voided:false },
  { id:5, gid:2, type:"coins", amt:-10, note:"Behavior deduction",            by:"Tyler",   time:"3:45 PM", voided:false },
  { id:6, gid:1, type:"stars", amt:1,   note:"Big Stick Award — Jamie S.",    by:"Tyler",   time:"4:00 PM", voided:false },
];

// ─── HELPERS ─────────────────────────────────────────────────────────────────
function useCoins(groups) {
  const totals = {};
  groups.forEach(g => { totals[g.id] = { coins: 0, stars: 0 }; });
  return totals;
}

// ─── MAIN APP ────────────────────────────────────────────────────────────────
export default function App() {
  const [groups, setGroups]           = useState(GROUPS.map(g => ({ ...g, coins: [142,98,167,115][g.id-1], stars: [3,2,4,2][g.id-1] })));
  const [txs, setTxs]                 = useState(SEED_TRANSACTIONS);
  const [view, setView]               = useState("Dashboard");
  const [txModal, setTxModal]         = useState(false);
  const [txForm, setTxForm]           = useState({ gid:"", type:"coins", amt:"", note:"" });
  const [toast, setToast]             = useState(null);
  const [blockModal, setBlockModal]   = useState(false);   // board overlay
  const [blockGroup, setBlockGroup]   = useState(null);
  const [blockPhase, setBlockPhase]   = useState("idle"); // idle|rolling|reveal|moving|done
  const [blockNumber, setBlockNumber] = useState(null);
  const [blockDisplay, setBlockDisplay] = useState(null);
  const [mgPhase, setMgPhase]         = useState("idle"); // idle|spinning|result
  const [mgIndex, setMgIndex]         = useState(0);
  const [mgResult, setMgResult]       = useState(null);
  const nextId = useRef(7);
  const spinRef = useRef(null);

  const showToast = (msg, col="#27AE60") => { setToast({msg,col}); setTimeout(()=>setToast(null),2800); };

  // ── Transaction submit ──
  const submitTx = () => {
    const amt = parseInt(txForm.amt); if (!txForm.gid || isNaN(amt)) return;
    const tx = { id: nextId.current++, gid: parseInt(txForm.gid), type: txForm.type, amt, note: txForm.note||"—", by:"Tyler", time: new Date().toLocaleTimeString([],{hour:"2-digit",minute:"2-digit"}), voided:false };
    setTxs(p=>[tx,...p]);
    setGroups(p=>p.map(g=> g.id!==tx.gid?g : tx.type==="coins"?{...g,coins:Math.max(0,g.coins+amt)}:{...g,stars:Math.max(0,g.stars+amt)}));
    setTxModal(false); setTxForm({gid:"",type:"coins",amt:"",note:""});
    showToast("✅ Transaction logged!");
  };

  const voidTx = (id) => {
    const tx = txs.find(t=>t.id===id); if(!tx||tx.voided) return;
    setTxs(p=>p.map(t=>t.id===id?{...t,voided:true}:t));
    setGroups(p=>p.map(g=>g.id!==tx.gid?g:tx.type==="coins"?{...g,coins:Math.max(0,g.coins-tx.amt)}:{...g,stars:Math.max(0,g.stars-tx.amt)}));
    showToast("↩️ Voided","#E74C3C");
  };

  // ── Block hit flow ──
  const startBlockHit = (gid) => {
    setBlockGroup(gid); setBlockPhase("rolling"); setBlockNumber(null); setBlockDisplay(null);
    let i = 0;
    const dest = SCRIPTED_DESTINATIONS[gid];
    const g = groups.find(x=>x.id===gid);
    const steps = ((dest - g.boardPos) + BOARD_SPACES.length) % BOARD_SPACES.length || BOARD_SPACES.length;
    // Roll dramatic numbers then land on `steps`
    const duration = 2200, start = Date.now();
    const roll = () => {
      const elapsed = Date.now()-start, prog = elapsed/duration;
      const delay = Math.max(40, prog*350);
      setBlockDisplay(Math.floor(Math.random()*12)+1);
      i++;
      if (elapsed < duration) { spinRef.current = setTimeout(roll, delay); }
      else {
        setBlockDisplay(steps);
        setBlockNumber(steps);
        setBlockPhase("reveal");
        setTimeout(()=>{
          setBlockPhase("moving");
          setGroups(p=>p.map(g=>g.id!==gid?g:{...g, boardPos: dest }));
          setTimeout(()=>setBlockPhase("done"),1200);
        },1400);
      }
    };
    spinRef.current = setTimeout(roll,80);
  };

  // ── Mini-game spinner ──
  const startMgSpin = () => {
    setMgPhase("spinning"); setMgResult(null);
    let i=0; const dur=3000, start=Date.now();
    const spin = () => {
      const el=Date.now()-start, prog=el/dur, delay=Math.max(50,prog*300);
      setMgIndex(i%MINIGAMES.length); i++;
      if(el<dur){ spinRef.current=setTimeout(spin,delay); }
      else { setMgResult(MINIGAMES[i%MINIGAMES.length]); setMgPhase("result"); }
    };
    spinRef.current=setTimeout(spin,80);
  };

  useEffect(()=>()=>clearTimeout(spinRef.current),[]);

  const sorted = [...groups].sort((a,b)=>(b.stars*10000+b.coins)-(a.stars*10000+a.coins));

  // ─── STYLES ───────────────────────────────────────────────────────────────
  const css = `
    @import url('https://fonts.googleapis.com/css2?family=Fredoka+One&family=Nunito:wght@400;600;700;800;900&display=swap');
    * { box-sizing: border-box; margin:0; padding:0; }
    body { background: #0e1628; }
    @keyframes twinkle  { 0%,100%{opacity:.15} 50%{opacity:.7} }
    @keyframes bounce   { 0%,100%{transform:translateY(0)} 50%{transform:translateY(-7px)} }
    @keyframes popIn    { 0%{transform:scale(.7) translateY(12px);opacity:0} 70%{transform:scale(1.06)} 100%{transform:scale(1);opacity:1} }
    @keyframes slideUp  { from{transform:translateY(18px);opacity:0} to{transform:translateY(0);opacity:1} }
    @keyframes shake    { 0%,100%{transform:rotate(0)} 20%{transform:rotate(-8deg)} 40%{transform:rotate(8deg)} 60%{transform:rotate(-6deg)} 80%{transform:rotate(5deg)} }
    @keyframes glow     { 0%,100%{box-shadow:0 0 12px #F39C1260} 50%{box-shadow:0 0 32px #F39C12cc, 0 0 60px #F39C1255} }
    @keyframes moveToken{ from{opacity:.4;transform:scale(.8)} to{opacity:1;transform:scale(1)} }
    @keyframes numberRoll { from{transform:translateY(-10px);opacity:0} to{transform:translateY(0);opacity:1} }
    @keyframes handSpin { 0%{transform:rotate(-20deg) scaleX(-1)} 50%{transform:rotate(20deg) scaleX(-1)} 100%{transform:rotate(-20deg) scaleX(-1)} }

    .app { min-height:100vh; background:linear-gradient(160deg,#0e1628 0%,#0a1f3f 60%,#0d2b1e 100%); font-family:'Nunito',sans-serif; color:#fff; position:relative; overflow:hidden; }
    .stars-bg { position:fixed; inset:0; pointer-events:none; z-index:0; }
    .star-dot { position:absolute; border-radius:50%; background:#fff; animation:twinkle 3s ease-in-out infinite alternate; }

    .header { position:relative; z-index:10; padding:16px 20px 0; }
    .logo { font-family:'Fredoka One',cursive; font-size:clamp(18px,4vw,26px); background:linear-gradient(90deg,#E74C3C,#F39C12,#27AE60,#3498DB); -webkit-background-clip:text; -webkit-text-fill-color:transparent; }
    .subtitle { font-size:10px; color:#667; letter-spacing:2px; font-weight:700; text-transform:uppercase; margin-top:2px; }

    .nav { display:flex; gap:6px; margin:14px 0 0; flex-wrap:wrap; }
    .nav-btn { font-family:'Nunito',sans-serif; font-size:12px; font-weight:800; padding:7px 14px; border-radius:22px; border:none; cursor:pointer; transition:all .2s; letter-spacing:.3px; }
    .nav-btn.off { background:rgba(255,255,255,.08); color:#aaa; }
    .nav-btn.off:hover { background:rgba(255,255,255,.14); color:#fff; }
    .nav-btn.on { color:#fff; box-shadow:0 4px 16px rgba(0,0,0,.4); }

    .content { position:relative; z-index:5; padding:18px 20px 48px; }

    .card { background:rgba(255,255,255,.055); border:1px solid rgba(255,255,255,.09); border-radius:16px; backdrop-filter:blur(8px); transition:all .2s; }
    .card:hover { background:rgba(255,255,255,.08); }

    .group-card { padding:16px; border-radius:16px; border-left-width:4px; border-left-style:solid; }
    .currency-pill { border-radius:10px; padding:10px 14px; text-align:center; }
    .coins-pill { background:rgba(243,156,18,.13); }
    .stars-pill { background:rgba(231,76,60,.13); }
    .currency-num { font-family:'Fredoka One',cursive; font-size:26px; line-height:1; }

    .btn { font-family:'Nunito',sans-serif; font-weight:800; border:none; border-radius:10px; cursor:pointer; padding:9px 18px; font-size:13px; transition:all .2s; }
    .btn:hover { transform:translateY(-1px); }
    .btn-red  { background:linear-gradient(135deg,#E74C3C,#C0392B); color:#fff; box-shadow:0 4px 14px rgba(231,76,60,.35); }
    .btn-gold { background:linear-gradient(135deg,#F39C12,#D68910); color:#fff; box-shadow:0 4px 14px rgba(243,156,18,.3); }
    .btn-green{ background:linear-gradient(135deg,#27AE60,#1E8449); color:#fff; }
    .btn-blue { background:linear-gradient(135deg,#3498DB,#2471A3); color:#fff; }
    .btn-ghost{ background:rgba(255,255,255,.1); border:1px solid rgba(255,255,255,.2); color:#fff; }

    input,select,textarea { background:rgba(255,255,255,.08); border:1px solid rgba(255,255,255,.18); border-radius:8px; color:#fff; padding:10px 13px; font-size:14px; font-family:'Nunito',sans-serif; width:100%; outline:none; transition:border-color .2s; }
    input:focus,select:focus { border-color:#F39C12; }
    select option { background:#0e1628; }
    label { font-size:11px; font-weight:700; color:#888; letter-spacing:1px; text-transform:uppercase; display:block; margin-bottom:5px; }

    .modal-bg { position:fixed; inset:0; background:rgba(0,0,0,.75); z-index:100; display:flex; align-items:center; justify-content:center; padding:20px; }
    .modal { background:#111d35; border:1px solid rgba(255,255,255,.13); border-radius:22px; padding:26px; width:100%; max-width:400px; animation:popIn .3s ease; }

    .block-wrap { display:flex; flex-direction:column; align-items:center; gap:10px; }
    .number-block { width:100px; height:100px; border-radius:18px; background:linear-gradient(135deg,#F39C12,#D68910); border:4px solid rgba(255,255,255,.35); display:flex; align-items:center; justify-content:center; font-family:'Fredoka One',cursive; font-size:54px; color:#fff; cursor:pointer; transition:all .25s; box-shadow:0 6px 0 #A04000, 0 8px 24px rgba(243,156,18,.4); transform:perspective(200px) rotateX(8deg); user-select:none; }
    .number-block.rolling { animation:glow .4s ease-in-out infinite; }
    .number-block.reveal  { animation:shake .4s ease; background:linear-gradient(135deg,#E74C3C,#C0392B); box-shadow:0 6px 0 #7B241C, 0 8px 30px rgba(231,76,60,.5); }
    .number-block.idle    { animation:bounce 2s ease-in-out infinite; }
    .number-block:hover.idle { transform:perspective(200px) rotateX(8deg) scale(1.06); }

    .path-line { stroke:rgba(255,255,255,.12); stroke-width:3; stroke-dasharray:8 6; fill:none; }
    .space-circle { transition:all .3s; cursor:default; }
    .token { transition:all .8s cubic-bezier(.34,1.56,.64,1); }

    .hand-anim { font-size:52px; display:inline-block; animation:handSpin 1s ease-in-out infinite; transform-origin:bottom center; }
    .mg-display { font-family:'Fredoka One',cursive; font-size:clamp(18px,4vw,28px); color:#F39C12; min-height:50px; display:flex; align-items:center; justify-content:center; text-align:center; animation:numberRoll .1s ease; }
  `;

  const NAV_ITEMS = [
    { id:"Dashboard", icon:"📊", color:"#E74C3C" },
    { id:"Board",     icon:"🗺️", color:"#F39C12" },
    { id:"Mini-Games",icon:"🎮", color:"#9B59B6" },
    { id:"Transactions",icon:"📋",color:"#3498DB" },
  ];

  return (
    <div className="app">
      <style>{css}</style>

      {/* Starfield */}
      <div className="stars-bg">
        {[...Array(45)].map((_,i)=>(
          <div key={i} className="star-dot" style={{ width:Math.random()*2.5+1, height:Math.random()*2.5+1, top:`${Math.random()*100}%`, left:`${Math.random()*100}%`, opacity:.3, animationDelay:`${Math.random()*4}s`, animationDuration:`${Math.random()*3+2}s` }} />
        ))}
      </div>

      {/* Header */}
      <div className="header">
        <div style={{display:"flex",alignItems:"center",justifyContent:"space-between"}}>
          <div style={{display:"flex",alignItems:"center",gap:12}}>
            <div style={{fontSize:32,animation:"bounce 2s ease-in-out infinite"}}>🏁</div>
            <div>
              <div className="logo">SUPER CLOT NOT PARTY '26</div>
              <div className="subtitle">Camp Clot Not · Staff Dashboard</div>
            </div>
          </div>
          <button className="btn btn-red" style={{fontSize:12}} onClick={()=>setTxModal(true)}>＋ Log Transaction</button>
        </div>
        <div className="nav">
          {NAV_ITEMS.map(n=>(
            <button key={n.id} className={`nav-btn ${view===n.id?"on":"off"}`}
              style={view===n.id?{background:`linear-gradient(135deg,${n.color}CC,${n.color}99)`}:{}}
              onClick={()=>setView(n.id)}>
              {n.icon} {n.id}
            </button>
          ))}
        </div>
      </div>

      <div className="content">

        {/* ══ DASHBOARD ══════════════════════════════════════════════════════ */}
        {view==="Dashboard" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase",marginBottom:14}}>🏆 Standings</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(260px,1fr))",gap:14,marginBottom:28}}>
              {sorted.map((g,rank)=>(
                <div key={g.id} className="card group-card" style={{borderLeftColor:g.color,animation:`slideUp .3s ease ${rank*.07}s both`}}>
                  <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:12}}>
                    <div style={{display:"flex",alignItems:"center",gap:10}}>
                      <div style={{width:34,height:34,borderRadius:"50%",background:g.color,display:"flex",alignItems:"center",justifyContent:"center",fontSize:17,boxShadow:`0 0 12px ${g.color}66`}}>{g.char}</div>
                      <div>
                        <div style={{fontWeight:800,fontSize:14}}>{g.name}</div>
                        <div style={{fontSize:11,color:"#556",marginTop:1}}>Space {g.boardPos} · {BOARD_SPACES[g.boardPos]?.icon} {BOARD_SPACES[g.boardPos]?.label}</div>
                      </div>
                    </div>
                    <div style={{background:rank===0?"linear-gradient(135deg,#F39C12,#E67E22)":"rgba(255,255,255,.08)",borderRadius:20,padding:"3px 12px",fontSize:12,fontWeight:900,color:rank===0?"#fff":"#666"}}>
                      #{rank+1}
                    </div>
                  </div>
                  <div style={{display:"flex",gap:10,marginBottom:12}}>
                    <div className="currency-pill coins-pill" style={{flex:1}}>
                      <div className="currency-num" style={{color:"#F39C12"}}>{g.coins}</div>
                      <div style={{fontSize:10,color:"#888",marginTop:2}}>🪙 COINS</div>
                    </div>
                    <div className="currency-pill stars-pill" style={{flex:1}}>
                      <div className="currency-num" style={{color:"#E74C3C",fontSize:22}}>{"⭐".repeat(Math.min(g.stars,5))}</div>
                      <div style={{fontSize:10,color:"#888",marginTop:2}}>STARS ({g.stars})</div>
                    </div>
                  </div>
                  <div style={{display:"flex",gap:8}}>
                    <button className="btn btn-gold" style={{flex:1,padding:"7px 0",fontSize:12}} onClick={()=>{setTxModal(true);setTxForm({gid:String(g.id),type:"coins",amt:"",note:""})}}>+ Coins</button>
                    <button className="btn btn-red"  style={{flex:1,padding:"7px 0",fontSize:12}} onClick={()=>{setTxModal(true);setTxForm({gid:String(g.id),type:"stars",amt:"",note:""})}}>+ Stars</button>
                  </div>
                </div>
              ))}
            </div>

            <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase",marginBottom:12}}>⚡ Recent</div>
            <div className="card" style={{overflow:"hidden"}}>
              {txs.filter(t=>!t.voided).slice(0,5).map((tx,i)=>{
                const g=groups.find(x=>x.id===tx.gid);
                return (
                  <div key={tx.id} style={{padding:"13px 16px",borderBottom:i<4?"1px solid rgba(255,255,255,.06)":"none",display:"flex",alignItems:"center",gap:12}}>
                    <div style={{width:34,height:34,borderRadius:9,background:tx.type==="coins"?"rgba(243,156,18,.18)":"rgba(231,76,60,.18)",display:"flex",alignItems:"center",justifyContent:"center",fontSize:17}}>
                      {tx.type==="coins"?"🪙":"⭐"}
                    </div>
                    <div style={{flex:1}}>
                      <span style={{color:g?.color,fontWeight:700,fontSize:13}}>{g?.name}</span>
                      <span style={{color:tx.amt>0?"#2ECC71":"#E74C3C",marginLeft:8,fontWeight:700,fontSize:13}}>{tx.amt>0?"+":""}{tx.amt} {tx.type}</span>
                      <div style={{fontSize:11,color:"#556",marginTop:1}}>{tx.note} · {tx.by} · {tx.time}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* ══ BOARD ══════════════════════════════════════════════════════════ */}
        {view==="Board" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:14}}>
              <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase"}}>🗺️ Game Board</div>
              <button className="btn btn-gold" style={{fontSize:12}} onClick={()=>{setBlockModal(true);setBlockPhase("idle");setBlockNumber(null);setBlockDisplay(null);setBlockGroup(null);}}>
                ⁉️ Block Hit
              </button>
            </div>

            {/* SVG Board */}
            <div className="card" style={{padding:12,marginBottom:20,overflow:"hidden"}}>
              <svg viewBox="0 0 600 430" style={{width:"100%",height:"auto",display:"block"}}>
                {/* Path lines connecting spaces */}
                <polyline className="path-line" points="60,385 130,385 200,385 270,385 340,385 410,385 480,385 540,320 480,255 410,255 340,255 270,255 200,255 130,255 60,255 60,185 130,185 200,185 270,120 340,120" />

                {/* Spaces */}
                {BOARD_SPACES.map(s=>{
                  const groupsHere = groups.filter(g=>g.boardPos===s.id);
                  return (
                    <g key={s.id}>
                      {/* Glow for star/bowser */}
                      {(s.type==="star"||s.type==="bowser") && <circle cx={s.x} cy={s.y} r={22} fill={s.color} opacity={.18} />}
                      <circle className="space-circle" cx={s.x} cy={s.y} r={18} fill={s.color} stroke="#0e1628" strokeWidth={3} opacity={.92} />
                      <text x={s.x} y={s.y+1} textAnchor="middle" dominantBaseline="middle" fontSize={13}>{s.icon}</text>
                      {/* Space number */}
                      <text x={s.x} y={s.y+28} textAnchor="middle" fontSize={8} fill="rgba(255,255,255,.35)">{s.id}</text>
                      {/* Group tokens */}
                      {groupsHere.map((g,gi)=>{
                        const offX = groupsHere.length>1?(gi-.5)*15:0;
                        return (
                          <g key={g.id} className="token" style={{animation:blockPhase==="moving"?"moveToken .6s ease":"none"}}>
                            <circle cx={s.x+offX} cy={s.y-26} r={10} fill={g.color} stroke="#fff" strokeWidth={2} />
                            <text x={s.x+offX} y={s.y-25} textAnchor="middle" dominantBaseline="middle" fontSize={9}>{g.char}</text>
                          </g>
                        );
                      })}
                    </g>
                  );
                })}

                {/* START label */}
                <text x="60" y="408" textAnchor="middle" fontSize={9} fontWeight="700" fill="#F39C12" fontFamily="Fredoka One,cursive">START</text>
              </svg>
            </div>

            {/* Legend */}
            <div style={{display:"flex",gap:8,flexWrap:"wrap",marginBottom:20}}>
              {[{c:"#9B59B6",i:"🎮",l:"Mini-Game"},{c:"#F39C12",i:"🪙",l:"Coin"},{c:"#E74C3C",i:"⭐",l:"Star Space"},{c:"#E74C3C",i:"👹",l:"Bowser!"}].map(x=>(
                <div key={x.l} style={{display:"flex",alignItems:"center",gap:6,background:"rgba(255,255,255,.06)",borderRadius:20,padding:"4px 12px",fontSize:11}}>
                  <div style={{width:8,height:8,borderRadius:"50%",background:x.c}}/>
                  {x.i} {x.l}
                </div>
              ))}
            </div>

            {/* Group positions list */}
            <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase",marginBottom:12}}>Group Positions</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(220px,1fr))",gap:10}}>
              {groups.map(g=>{
                const sp=BOARD_SPACES[g.boardPos];
                return (
                  <div key={g.id} className="card" style={{padding:"12px 14px",display:"flex",alignItems:"center",gap:10,borderLeft:`3px solid ${g.color}`}}>
                    <div style={{fontSize:22}}>{g.char}</div>
                    <div style={{flex:1}}>
                      <div style={{fontWeight:800,fontSize:13,color:g.color}}>{g.short}</div>
                      <div style={{fontSize:11,color:"#667",marginTop:2}}>{sp?.icon} Space {g.boardPos}: {sp?.label}</div>
                    </div>
                    <div style={{fontSize:11,fontWeight:700,color:"#F39C12"}}>🪙{g.coins} ⭐{g.stars}</div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* ══ MINI-GAMES ═════════════════════════════════════════════════════ */}
        {view==="Mini-Games" && (
          <div style={{animation:"slideUp .3s ease",textAlign:"center"}}>
            <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase",marginBottom:6}}>🎮 Evening Challenge Picker</div>
            <div style={{fontSize:13,color:"#667",marginBottom:32}}>Minute to Win It &amp; group challenges — the big nightly events.</div>

            {/* Hand + spinner display */}
            <div style={{display:"flex",flexDirection:"column",alignItems:"center",gap:20,marginBottom:36}}>
              <div style={{position:"relative",background:"rgba(255,255,255,.05)",border:"1px solid rgba(255,255,255,.1)",borderRadius:24,padding:"32px 40px",maxWidth:480,width:"100%"}}>
                {mgPhase==="idle" && (
                  <div>
                    <div style={{fontSize:64,marginBottom:12}}>🫳</div>
                    <div style={{fontSize:18,color:"#aaa",fontFamily:"'Fredoka One',cursive"}}>Ready to spin?</div>
                    <div style={{fontSize:12,color:"#556",marginTop:6}}>Tap the button below to reveal tonight's challenge</div>
                  </div>
                )}
                {mgPhase==="spinning" && (
                  <div>
                    <div className="hand-anim" style={{marginBottom:16}}>🫳</div>
                    <div className="mg-display">{MINIGAMES[mgIndex]}</div>
                    <div style={{fontSize:12,color:"#F39C12",marginTop:10,fontWeight:700,letterSpacing:1}}>SPINNING...</div>
                  </div>
                )}
                {mgPhase==="result" && (
                  <div style={{animation:"popIn .4s ease"}}>
                    <div style={{fontSize:42,marginBottom:10}}>🎉</div>
                    <div style={{fontSize:13,color:"#27AE60",fontWeight:700,letterSpacing:2,textTransform:"uppercase",marginBottom:10}}>Tonight's Challenge!</div>
                    <div style={{fontFamily:"'Fredoka One',cursive",fontSize:clamp(22),color:"#fff",lineHeight:1.3}}>{mgResult}</div>
                  </div>
                )}
              </div>

              <div style={{display:"flex",gap:12}}>
                {mgPhase!=="spinning" && (
                  <button className="btn btn-gold" style={{fontSize:15,padding:"12px 32px"}} onClick={startMgSpin}>
                    {mgPhase==="result"?"🔄 Spin Again":"🎲 Spin!"}
                  </button>
                )}
                {mgPhase==="result" && (
                  <button className="btn btn-ghost" onClick={()=>{setMgPhase("idle");setMgResult(null);}}>Reset</button>
                )}
              </div>
            </div>

            {/* Activity list */}
            <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase",marginBottom:12,textAlign:"left"}}>All Challenges</div>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(200px,1fr))",gap:10}}>
              {MINIGAMES.map((mg,i)=>(
                <div key={i} className="card" style={{padding:"12px 14px",display:"flex",alignItems:"center",gap:10,textAlign:"left"}}>
                  <div style={{width:32,height:32,borderRadius:8,background:"rgba(155,89,182,.2)",display:"flex",alignItems:"center",justifyContent:"center",fontSize:16}}>🎮</div>
                  <div style={{fontSize:13,fontWeight:600,lineHeight:1.3}}>{mg}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ══ TRANSACTIONS ═══════════════════════════════════════════════════ */}
        {view==="Transactions" && (
          <div style={{animation:"slideUp .3s ease"}}>
            <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:16}}>
              <div style={{fontSize:11,fontWeight:700,color:"#556",letterSpacing:2,textTransform:"uppercase"}}>📋 Transaction Log</div>
              <button className="btn btn-red" style={{fontSize:12}} onClick={()=>setTxModal(true)}>＋ New</button>
            </div>
            <div className="card" style={{overflow:"hidden"}}>
              {txs.map((tx,i)=>{
                const g=groups.find(x=>x.id===tx.gid);
                return (
                  <div key={tx.id} style={{padding:"13px 16px",borderBottom:i<txs.length-1?"1px solid rgba(255,255,255,.05)":"none",display:"flex",alignItems:"center",gap:12,opacity:tx.voided?.4:1}}>
                    <div style={{width:34,height:34,borderRadius:9,background:tx.type==="coins"?"rgba(243,156,18,.18)":"rgba(231,76,60,.18)",display:"flex",alignItems:"center",justifyContent:"center",fontSize:17,flexShrink:0}}>
                      {tx.type==="coins"?"🪙":"⭐"}
                    </div>
                    <div style={{flex:1,minWidth:0}}>
                      <div style={{fontSize:13,fontWeight:700,display:"flex",alignItems:"center",gap:6,flexWrap:"wrap"}}>
                        <span style={{color:g?.color}}>{g?.name}</span>
                        <span style={{color:tx.amt>0?"#2ECC71":"#E74C3C"}}>{tx.amt>0?"+":""}{tx.amt} {tx.type}</span>
                        {tx.voided&&<span style={{fontSize:10,color:"#E74C3C",background:"rgba(231,76,60,.15)",borderRadius:4,padding:"1px 6px",fontWeight:700}}>VOIDED</span>}
                      </div>
                      <div style={{fontSize:11,color:"#556",marginTop:2}}>{tx.note} · {tx.by} · {tx.time}</div>
                    </div>
                    {!tx.voided&&(
                      <button onClick={()=>voidTx(tx.id)} style={{background:"rgba(231,76,60,.12)",border:"1px solid rgba(231,76,60,.25)",borderRadius:7,color:"#E74C3C",padding:"5px 10px",cursor:"pointer",fontSize:11,fontWeight:700,flexShrink:0,fontFamily:"Nunito,sans-serif"}}>Void</button>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* ══ BLOCK HIT MODAL ════════════════════════════════════════════════ */}
      {blockModal && (
        <div className="modal-bg" onClick={e=>e.target===e.currentTarget&&blockPhase==="idle"&&setBlockModal(false)}>
          <div className="modal" style={{maxWidth:460}}>
            <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:20}}>
              <div style={{fontFamily:"'Fredoka One',cursive",fontSize:22,color:"#F39C12"}}>⁉️ Block Hit!</div>
              {blockPhase==="idle"&&<button className="btn btn-ghost" style={{fontSize:12,padding:"5px 12px"}} onClick={()=>setBlockModal(false)}>✕</button>}
            </div>

            {/* Group selector */}
            {(blockPhase==="idle"||blockPhase==="done") && (
              <div style={{marginBottom:20}}>
                <label>Which group is hitting?</label>
                <div style={{display:"flex",gap:8,flexWrap:"wrap",marginTop:6}}>
                  {groups.map(g=>(
                    <button key={g.id} onClick={()=>{setBlockGroup(g.id);setBlockPhase("idle");setBlockDisplay(null);}} style={{
                      background:blockGroup===g.id?g.color:"rgba(255,255,255,.08)",
                      border:`2px solid ${blockGroup===g.id?g.color:"rgba(255,255,255,.15)"}`,
                      borderRadius:10,color:"#fff",padding:"8px 14px",cursor:"pointer",fontWeight:700,fontSize:13,transition:"all .2s",fontFamily:"Nunito,sans-serif",
                      boxShadow:blockGroup===g.id?`0 3px 16px ${g.color}55`:"none"
                    }}>{g.char} {g.short}</button>
                  ))}
                </div>
              </div>
            )}

            {/* The Block */}
            <div className="block-wrap">
              <div
                className={`number-block ${blockPhase==="rolling"?"rolling":blockPhase==="reveal"?"reveal":"idle"}`}
                onClick={()=>{ if(blockGroup&&blockPhase==="idle") startBlockHit(blockGroup); }}
                style={{cursor:blockGroup&&blockPhase==="idle"?"pointer":"default"}}
              >
                {blockDisplay ?? "⁉️"}
              </div>

              {blockPhase==="idle"&&!blockGroup&&<div style={{fontSize:12,color:"#667"}}>Select a group to begin</div>}
              {blockPhase==="idle"&&blockGroup&&<div style={{fontSize:12,color:"#F39C12",fontWeight:700}}>Tap the block!</div>}
              {blockPhase==="rolling"&&<div style={{fontSize:12,color:"#F39C12",fontWeight:700,letterSpacing:1}}>ROLLING...</div>}

              {blockPhase==="reveal"&&blockNumber&&(
                <div style={{textAlign:"center",animation:"popIn .4s ease"}}>
                  <div style={{fontFamily:"'Fredoka One',cursive",fontSize:18,color:"#fff",marginBottom:4}}>
                    Moves <span style={{color:"#E74C3C",fontSize:26}}>{blockNumber}</span> spaces!
                  </div>
                  <div style={{fontSize:12,color:"#aaa"}}>Moving token to space {SCRIPTED_DESTINATIONS[blockGroup]}...</div>
                </div>
              )}

              {blockPhase==="moving"&&(
                <div style={{textAlign:"center"}}>
                  <div style={{fontSize:24,animation:"bounce .4s ease-in-out infinite"}}>🏃</div>
                  <div style={{fontSize:12,color:"#F39C12",fontWeight:700,marginTop:4}}>Moving...</div>
                </div>
              )}

              {blockPhase==="done"&&(
                <div style={{textAlign:"center",animation:"popIn .4s ease"}}>
                  {(()=>{
                    const g=groups.find(x=>x.id===blockGroup);
                    const sp=BOARD_SPACES[g?.boardPos];
                    return (
                      <>
                        <div style={{fontSize:32,marginBottom:6}}>{sp?.icon}</div>
                        <div style={{fontFamily:"'Fredoka One',cursive",fontSize:18,color:"#27AE60"}}>Landed on {sp?.label}!</div>
                        <div style={{fontSize:12,color:"#667",marginTop:4}}>Space {g?.boardPos} · {g?.name}</div>
                        <div style={{display:"flex",gap:8,marginTop:16,justifyContent:"center"}}>
                          <button className="btn btn-ghost" style={{fontSize:12}} onClick={()=>{setBlockPhase("idle");setBlockDisplay(null);setBlockNumber(null);}}>Another Group</button>
                          <button className="btn btn-green" style={{fontSize:12}} onClick={()=>{setBlockModal(false);setBlockPhase("idle");setBlockDisplay(null);setBlockNumber(null);setView("Board");}}>View Board</button>
                        </div>
                      </>
                    );
                  })()}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ══ TRANSACTION MODAL ══════════════════════════════════════════════ */}
      {txModal && (
        <div className="modal-bg" onClick={e=>e.target===e.currentTarget&&setTxModal(false)}>
          <div className="modal">
            <div style={{fontFamily:"'Fredoka One',cursive",fontSize:20,marginBottom:20,color:"#fff"}}>Log Transaction</div>
            <div style={{display:"flex",flexDirection:"column",gap:14}}>
              <div><label>Group</label>
                <select value={txForm.gid} onChange={e=>setTxForm(f=>({...f,gid:e.target.value}))}>
                  <option value="">Select a group...</option>
                  {groups.map(g=><option key={g.id} value={g.id}>{g.char} {g.name}</option>)}
                </select>
              </div>
              <div><label>Type</label>
                <select value={txForm.type} onChange={e=>setTxForm(f=>({...f,type:e.target.value}))}>
                  <option value="coins">🪙 Coins</option>
                  <option value="stars">⭐ Stars</option>
                </select>
              </div>
              <div><label>Amount (negative to deduct)</label>
                <input type="number" placeholder="e.g. 25 or -10" value={txForm.amt} onChange={e=>setTxForm(f=>({...f,amt:e.target.value}))} />
              </div>
              <div><label>Note (optional)</label>
                <input type="text" placeholder="e.g. Won Bowser Dodge Battle" value={txForm.note} onChange={e=>setTxForm(f=>({...f,note:e.target.value}))} />
              </div>
              <div style={{display:"flex",gap:10,marginTop:4}}>
                <button className="btn btn-ghost" style={{flex:1}} onClick={()=>setTxModal(false)}>Cancel</button>
                <button className="btn btn-red" style={{flex:2}} onClick={submitTx}>Log It</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Toast */}
      {toast&&(
        <div style={{position:"fixed",bottom:24,left:"50%",transform:"translateX(-50%)",background:toast.col,color:"#fff",padding:"11px 22px",borderRadius:12,fontWeight:700,fontSize:14,zIndex:200,animation:"popIn .3s ease",boxShadow:"0 4px 20px rgba(0,0,0,.4)",fontFamily:"Nunito,sans-serif",whiteSpace:"nowrap"}}>
          {toast.msg}
        </div>
      )}
    </div>
  );
}

function clamp(size) { return `clamp(18px,4vw,${size}px)`; }
