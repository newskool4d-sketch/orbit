'use strict';
const $=s=>document.querySelector(s),$$=s=>Array.from(document.querySelectorAll(s));
const icons={orbit:'orbit',codex:'code',claude:'claude',drive:'file',obsidian:'gem'};
const labels={calendar:'Calendar',tasks:'Tasks',drive:'Drive',obsidian:'Obsidian',codex:'Codex',claude:'Claude Code'};
const icon=name=>`<svg aria-hidden="true"><use href="#icon-${icons[name]||name}"/></svg>`;
const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let state={files:[],agents:[],events:[],tasks:[],ddays:[],statuses:[],settings:{},theme:'moss',pinned:false,refreshing:true};
let activeTab='today',fileSource='all',agentProvider='all',showAllTasks=false,showArchivedDdays=false,editingDdayId='',counter=0,toastTimer,ddayReturnFocus;
let ddayBusy=false,ddayFormRevision=0,ddayListMarkup='',ddayMidnightTimer;
const pending=new Map(),busyTasks=new Set();
function send(action,args={}){return new Promise((resolve,reject)=>{if(!window.orbitPlatform?.canSend()){reject(new Error('실제 연동은 Orbit 앱에서 사용할 수 있습니다.'));return}const request=String(++counter);const timeout=setTimeout(()=>{pending.delete(request);reject(new Error('응답이 늦어지고 있습니다. 새로고침 후 상태를 확인하세요.'))},action==='connectGoogle'?210000:180000);pending.set(request,{resolve,reject,timeout});try{window.orbitPlatform.post({action,request,...args})}catch(error){clearTimeout(timeout);pending.delete(request);reject(error)}})}
async function act(action,args={}){try{const message=await send(action,args);if(message)toast(message);return true}catch(e){toast(e.message);return false}}
function toast(text){$('#toast').textContent=text;$('#toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('#toast').hidden=true,6500)}
window.orbit={receive(message){if(message.kind==='reply'){const job=pending.get(message.request);if(job){clearTimeout(job.timeout);pending.delete(message.request);message.error?job.reject(new Error(message.error)):job.resolve(message.message)}return}if(message.kind==='snapshot'){state=message;render()}}};
function relative(stamp){const seconds=Math.max(0,Date.now()/1000-Number(stamp));if(!stamp)return '시간 미상';if(seconds<60)return '방금 전';if(seconds<3600)return `${Math.floor(seconds/60)}분 전`;if(seconds<86400)return `${Math.floor(seconds/3600)}시간 전`;if(seconds<86400*7)return `${Math.floor(seconds/86400)}일 전`;return new Date(stamp*1000).toLocaleDateString('ko-KR',{month:'short',day:'numeric'})}
function dayKey(date=new Date()){return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`}
function dayOrdinal(value){const match=/^(\d{4})-(\d{2})-(\d{2})$/.exec(value||'');if(!match)return NaN;const year=Number(match[1]),month=Number(match[2]),day=Number(match[3]),date=new Date(0);date.setUTCHours(0,0,0,0);date.setUTCFullYear(year,month-1,day);const stamp=date.getTime();return year>=1&&date.getUTCFullYear()===year&&date.getUTCMonth()===month-1&&date.getUTCDate()===day?stamp/86400000:NaN}
function ddayDelta(target,today=dayKey()){return dayOrdinal(target)-dayOrdinal(today)}
function ddayLabel(target,today=dayKey()){const delta=ddayDelta(target,today);return delta===0?'D-day':delta>0?`D-${delta}`:`D+${Math.abs(delta)}`}
function ddaySort(items,today=dayKey()){return [...items].sort((a,b)=>Number(Boolean(b.pinned))-Number(Boolean(a.pinned))||Number(ddayDelta(a.targetDate,today)<0)-Number(ddayDelta(b.targetDate,today)<0)||Math.abs(ddayDelta(a.targetDate,today))-Math.abs(ddayDelta(b.targetDate,today))||String(a.createdAt).localeCompare(String(b.createdAt)))}
function ddayDateText(item,today=dayKey()){const delta=ddayDelta(item.targetDate,today),[year,month,day]=item.targetDate.split('-').map(Number),date=new Date(0);date.setFullYear(year,month-1,day);date.setHours(12,0,0,0);const label=date.toLocaleDateString('ko-KR',{month:'long',day:'numeric'});return delta===0?'오늘':delta<0?`${Math.abs(delta)}일 지남`:label}
function ddayCard(item,manage=false){const delta=ddayDelta(item.targetDate),status=delta===0?'today':delta<0?'past':'future',title=esc(item.title),pin=item.pinned?'중요 · ':'';if(!manage)return `<button class="dday-card ${status}" data-dday-edit="${esc(item.id)}" title="${title} 편집"><span class="dday-count">${esc(ddayLabel(item.targetDate))}</span><span class="dday-copy"><strong>${title}</strong><small>${pin}${esc(ddayDateText(item))}</small></span>${icon('chevron')}</button>`;return `<article class="dday-manage ${item.archived?'archived':''}"><div class="dday-main"><span class="dday-count ${status}">${esc(ddayLabel(item.targetDate))}</span><span class="dday-copy"><strong>${title}</strong><small>${esc(item.targetDate)} · ${esc(ddayDateText(item))}${item.pinned?' · 중요':''}</small></span></div><div class="dday-actions"><button class="text" type="button" data-dday-action="pin" data-dday-id="${esc(item.id)}">${item.pinned?'중요 해제':'중요'}</button><button class="text" type="button" data-dday-edit="${esc(item.id)}">편집</button><button class="text" type="button" data-dday-action="archive" data-dday-id="${esc(item.id)}">${item.archived?'복원':'보관'}</button><button class="text danger" type="button" data-dday-delete="${esc(item.id)}">삭제</button></div><div class="dday-delete-confirm" data-dday-confirm="${esc(item.id)}" hidden><span>‘${title}’을(를) 삭제할까요?</span><button class="secondary danger" type="button" data-dday-action="delete" data-dday-id="${esc(item.id)}">삭제</button><button class="text" type="button" data-dday-delete-cancel="${esc(item.id)}">취소</button></div></article>`}
function time(stamp){return new Date(stamp*1000).toLocaleTimeString('ko-KR',{hour:'2-digit',minute:'2-digit',hour12:false})}
function status(source){return state.statuses.find(s=>s.source===source)||{state:'disconnected',updated:0,message:'연결 대기'}}
function sameDay(stamp){return dayKey(new Date(stamp*1000))===dayKey()}
function calendarGroupStamp(event){const date=new Date(event.start*1000),today=new Date(),start=new Date(date.getFullYear(),date.getMonth(),date.getDate()),todayStart=new Date(today.getFullYear(),today.getMonth(),today.getDate());return Math.max(start.getTime(),todayStart.getTime())/1000}
function calendarDateLabel(stamp){const date=new Date(stamp*1000),today=new Date(),days=Math.round((Date.UTC(date.getFullYear(),date.getMonth(),date.getDate())-Date.UTC(today.getFullYear(),today.getMonth(),today.getDate()))/86400000),label=date.toLocaleDateString('ko-KR',{month:'long',day:'numeric',weekday:'short'});return days===0?`오늘 · ${label}`:days===1?`내일 · ${label}`:label}
function calendarTimeLabel(event){if(event.allDay)return '종일';const start=new Date(event.start*1000),end=new Date(event.end*1000);return dayKey(start)===dayKey(end)?`${time(event.start)} – ${time(event.end)}`:`${time(event.start)} – ${end.toLocaleDateString('ko-KR',{month:'short',day:'numeric'})} ${time(event.end)}`}
function warning(source){const s=status(source);return s.state==='error'?`<div class="source-warning">${esc(s.message)}${s.updated?` · 마지막 동기화 ${relative(s.updated)}`:''}</div>`:''}
function empty(text,settings=false){return `<div class="empty">${esc(text)}${settings?'<br><button class="secondary" data-action="settings">연결 설정</button>':''}</div>`}
function fileCard(file){return `<button class="file" data-file="${esc(file.id)}" title="${esc(file.name)}"><span class="file-icon ${esc(file.source)}">${icon(file.source)}</span><span class="file-detail"><span class="file-name">${esc(file.name)}</span><span class="file-meta">${labels[file.source]} · ${relative(file.modified)}</span></span></button>`}
function agentCard(a){const running=a.status==='진행 중',actionLabel=window.orbitPlatform.agentActionLabel(a,state);return `<button class="agent" data-agent="${esc(a.id)}" title="${esc(actionLabel)}" ${a.openMode==='unavailable'?'disabled aria-disabled="true"':''}><span class="agent-provider ${esc(a.provider)}">${icon(a.provider)}${labels[a.provider]}</span><span class="agent-title">${esc(a.title)}</span><span class="agent-project">${esc(a.project)} · ${relative(a.updated)}</span><span class="agent-bottom"><span class="state ${running?'running':''}">${running?'<i class="dot"></i>':''}${esc(a.status)}</span>${icon('arrow')}</span>${a.openMode?`<span class="agent-action">${esc(actionLabel)}</span>`:''}</button>`}
function render(){
 const platform=state.platform||window.orbitPlatform.name,textSizeValue=state.settings?.textSize||document.body.dataset.textSize,textSize=['normal','large','xlarge'].includes(textSizeValue)?textSizeValue:'normal';document.body.dataset.platform=platform;document.body.dataset.textSize=textSize;$$('[data-shortcut]').forEach(node=>node.textContent=window.orbitPlatform.shortcut(state));$('#platform-version').textContent=`Orbit 1.0 · ${platform==='windows'?'Windows':'이 맥'}에서 실행`;
 document.body.dataset.theme=state.theme;$('#pin').setAttribute('aria-pressed',String(state.pinned));$('#refresh').classList.toggle('spinning',state.refreshing);$('#refresh').disabled=state.refreshing;
 const now=new Date();$('#date').textContent=now.toLocaleDateString('ko-KR',{month:'long',day:'numeric',weekday:'short'});const hour=now.getHours();$('#greeting').textContent=hour<6?'고요한 새벽이에요.':hour<12?'좋은 아침이에요.':hour<18?'좋은 오후예요.':'편안한 저녁이에요.';
 const todayEvents=state.events.filter(e=>e.start<new Date(now.getFullYear(),now.getMonth(),now.getDate()+1).getTime()/1000&&e.end>new Date(now.getFullYear(),now.getMonth(),now.getDate()).getTime()/1000);
 const done=state.tasks.filter(t=>t.completed).length,total=state.tasks.length;
 $('#summary').textContent=status('tasks').state==='connected'?`오늘 일정 ${todayEvents.length}개, 남은 할 일 ${total-done}개. 차근차근 시작해볼까요?`:`최근 파일 ${state.files.length}개와 AI 작업 ${state.agents.length}개가 모였어요.`;
 $('#progress-count').textContent=state.settings.googleConnected?(total?`${done}/${total}`:'0'):'—';$('#progress-ring').style.strokeDashoffset=String(113.1*(1-(total?done/total:0)));$('.progress').setAttribute('aria-label',state.settings.googleConnected?`할 일 ${total}개 중 ${done}개 완료`:'Google Tasks 미연결');
 $('#calendar-count').textContent=state.events.length;$('#agent-count').textContent=state.agents.length;renderToday(todayEvents);renderDdays();renderCalendar();renderFiles();renderAgents();renderSettings();renderDdayManager();scheduleDdayMidnight();
 $('#connections').innerHTML=['calendar','drive','obsidian','codex','claude'].map(s=>`<button class="${esc(status(s).state)}" data-action="settings" title="${esc(status(s).message)}"><i class="dot"></i>${s==='calendar'?'Google':s==='claude'?'Claude':labels[s]}</button>`).join('');
}
function renderDdays(){const root=$('#dday-section');if(!state.capabilities?.dday){root.hidden=true;return}root.hidden=false;const items=ddaySort((state.ddays||[]).filter(item=>!item.archived)),visible=items.slice(0,3);let html='<div class="section-heading"><h2>중요 날짜 <small>이 기기에 저장</small></h2><button class="text" data-dday-open="true">전체 보기 '+icon('chevron')+'</button></div>';if(state.ddayError)html+=`<p class="source-warning" role="alert">${esc(state.ddayError)}</p>`;if(visible.length)html+=`<div class="dday-list">${visible.map(item=>ddayCard(item)).join('')}</div>${items.length>3?`<p class="subtle">외 ${items.length-3}개 · 전체 보기에서 확인하세요.</p>`:''}`;else if(!state.ddayError)html+='<div class="empty">중요한 날짜를 직접 등록해 보세요.<br><button class="secondary" data-dday-open="true">날짜 추가</button></div>';root.innerHTML=html}
function renderDdayManager(){
 if(!state.capabilities?.dday)return;
 const error=$('#dday-storage-error');error.hidden=!state.ddayError;error.textContent=state.ddayError||'';
 const items=ddaySort((state.ddays||[]).filter(item=>Boolean(item.archived)===showArchivedDdays));
 const markup=items.map(item=>ddayCard(item,true)).join('')||`<div class="empty">${showArchivedDdays?'보관한 날짜가 없습니다.':'등록한 날짜가 없습니다.'}</div>`;
 if(markup!==ddayListMarkup){$('#dday-list').innerHTML=markup;ddayListMarkup=markup}
 $('#dday-archived-toggle').textContent=showArchivedDdays?'등록 목록 보기':'보관함 보기';syncDdayControls();
}
function resetDdayForm(){ddayFormRevision++;editingDdayId='';$('#dday-form').reset();$('#dday-id').value='';$('#dday-save').textContent='날짜 추가';$('#dday-cancel').hidden=true;$('#dday-form-error').hidden=true}
function openDdayManager(id=''){
 if(!state.capabilities?.dday)return;
 if($('#dday-manager').hidden)ddayReturnFocus=document.activeElement;
 $('#dday-manager').hidden=false;setDdayBackground(true);resetDdayForm();renderDdayManager();
 const item=(state.ddays||[]).find(value=>value.id===id);
 if(item){editingDdayId=id;$('#dday-id').value=id;$('#dday-title').value=item.title;$('#dday-date').value=item.targetDate;$('#dday-pinned').checked=Boolean(item.pinned);$('#dday-save').textContent='변경 저장';$('#dday-cancel').hidden=false}
 (ddayBusy||state.ddayError?$('#dday-close'):$('#dday-title')).focus();
}
function closeDdayManager(){resetDdayForm();$('#dday-manager').hidden=true;setDdayBackground(false);(ddayReturnFocus?.isConnected?ddayReturnFocus:$('#dday-section [data-dday-open]')||$('#settings-button')).focus()}
function ddayFormError(message){const node=$('#dday-form-error');node.textContent=message;node.hidden=false}

function setDdayBackground(value){for(const selector of ['header','.greeting','nav','#content','footer','#settings'])$(selector).inert=value}
function syncDdayControls(){const disabled=ddayBusy||Boolean(state.ddayError);for(const selector of ['#dday-title','#dday-date','#dday-pinned','#dday-save','#dday-cancel'])$(selector).disabled=disabled;$$('#dday-list button').forEach(node=>node.disabled=disabled)}
async function mutateDday(action,args){
 if(ddayBusy||state.ddayError)return false;
 ddayBusy=true;syncDdayControls();
 try{return await act(action,args)}finally{ddayBusy=false;syncDdayControls()}
}
async function submitDday(e){
 e.preventDefault();if(ddayBusy||state.ddayError)return;
 const title=$('#dday-title').value.trim(),targetDate=$('#dday-date').value,pinned=$('#dday-pinned').checked,revision=ddayFormRevision;
 if(!title||title.length>120||/[\u0000-\u001f\u007f-\u009f]/.test(title)){ddayFormError('제목은 제어 문자 없이 1자 이상 120자 이하로 입력하세요.');$('#dday-title').focus();return}
 if(!Number.isFinite(dayOrdinal(targetDate))){ddayFormError('실제 존재하는 날짜를 선택하세요.');$('#dday-date').focus();return}
 $('#dday-form-error').hidden=true;
 const ok=await mutateDday(editingDdayId?'updateDday':'addDday',{...(editingDdayId?{id:editingDdayId}:{}),title,targetDate,pinned});
 if(revision!==ddayFormRevision)return;
 if(ok){resetDdayForm();showArchivedDdays=false;renderDdayManager();$('#dday-title').focus()}
 else ddayFormError('저장하지 못했습니다. 입력 내용을 유지했으니 오류를 확인한 뒤 다시 시도하세요.');
}
async function handleDdayAction(button){
 const id=button.dataset.ddayId,item=(state.ddays||[]).find(value=>value.id===id),action=button.dataset.ddayAction,revision=ddayFormRevision;
 if(!item||ddayBusy)return;
 let ok=false;
 if(action==='pin')ok=await mutateDday('updateDday',{id,title:item.title,targetDate:item.targetDate,pinned:!item.pinned});
 if(action==='archive')ok=await mutateDday('archiveDday',{id,value:!item.archived});
 if(action==='delete')ok=await mutateDday('deleteDday',{id});
 if(ok&&revision===ddayFormRevision){if(editingDdayId===id)resetDdayForm();if(!$('#dday-manager').hidden)$('#dday-archived-toggle').focus()}
}
function trapDdayFocus(e){
 if($('#dday-manager').hidden||e.key!=='Tab')return false;
 const nodes=Array.from($('#dday-manager').querySelectorAll('button:not(:disabled),input:not(:disabled):not([type="hidden"])')).filter(node=>!node.closest('[hidden]'));
 if(!nodes.length)return false;
 const first=nodes[0],last=nodes[nodes.length-1];
 if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus()}
 else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus()}
 return true;
}
function scheduleDdayMidnight(){
 clearTimeout(ddayMidnightTimer);
 if(document.hidden||!state.capabilities?.dday)return;
 const now=new Date(),next=new Date(now.getFullYear(),now.getMonth(),now.getDate()+1);
 ddayMidnightTimer=setTimeout(refreshDdayClock,next.getTime()-now.getTime()+50);
}
function refreshDdayClock(){if(!document.hidden){renderDdays();renderDdayManager()}scheduleDdayMidnight()}

function renderToday(todayEvents){
 const upcoming=state.events.find(e=>!e.allDay&&e.end>Date.now()/1000)||state.events.find(e=>e.end>Date.now()/1000);
 if(!state.settings.googleConnected){$('#focus').innerHTML=`<div class="focus"><div class="focus-text"><p class="overline">${icon('calendar')} YOUR DAY, CONNECTED</p><h2>하루의 흐름을 연결하세요.</h2><p class="meta">Google 일정과 할 일을 이곳에서 한눈에.</p></div><button class="primary" data-action="settings">Google 연결 ${icon('arrow')}</button></div>`}
 else if(upcoming){const delta=Math.ceil((upcoming.start-Date.now()/1000)/60);const datePrefix=sameDay(upcoming.start)?'':new Date(upcoming.start*1000).toLocaleDateString('ko-KR',{month:'short',day:'numeric'})+' · ';const meta=upcoming.allDay?'종일':`${datePrefix}${time(upcoming.start)} – ${time(upcoming.end)}`,hasMeeting=window.orbitPlatform.hasMeeting(upcoming);$('#focus').innerHTML=`<article class="focus"><div class="focus-text"><p class="overline"><i class="dot"></i>${delta<=0?'NOW':'UP NEXT'} ${delta>0&&delta<60?`· ${delta}분 후`:''}</p><h2>${esc(upcoming.title)}</h2><p class="meta">${esc(meta)} ${hasMeeting?icon('video'):''}</p></div><button class="primary" data-event="${esc(upcoming.id)}" data-meeting="${hasMeeting}">${hasMeeting?'회의 참여':'일정 열기'} ${icon('arrow')}</button></article>`}
 else{$('#focus').innerHTML=`<div class="focus"><div class="focus-text"><p class="overline">A LITTLE ROOM TO FOCUS</p><h2>${status('calendar').state==='error'?'일정을 불러오지 못했어요.':'잠시, 집중하기 좋은 시간.'}</h2><p class="meta">${status('calendar').state==='error'?'연결 상태에서 자세한 내용을 확인하세요.':'앞으로 7일간 표시할 일정이 없습니다.'}</p></div><button class="secondary" data-action="openCalendar">캘린더 열기</button></div>`}
 $('#focus').insertAdjacentHTML('beforeend',warning('calendar'));
 const additional=todayEvents.filter(e=>e.id!==upcoming?.id&&e.end>Date.now()/1000).slice(0,2);if(additional.length){$('#focus').insertAdjacentHTML('beforeend','<div class="agenda">'+additional.map(e=>`<button data-event="${esc(e.id)}"><time>${e.allDay?'종일':time(e.start)}</time><span>${esc(e.title)}</span></button>`).join('')+'</div>')}
 let taskHTML=`<div class="section-heading"><h2>${showAllTasks?'할 일 목록':'오늘 할 일'} <small>Google Tasks</small></h2><button class="text" id="all-tasks">${showAllTasks?'오늘만 보기':'전체 보기'} ${icon('chevron')}</button></div>`;
 if(!state.settings.googleConnected){taskHTML+='<p class="quiet">계정을 연결하면 오늘 할 일과 완료 상태가 표시됩니다.</p>'}
 else{const selected=showAllTasks?state.tasks:state.tasks.filter(t=>!t.completed&&(!t.due||t.due<=dayKey()));const visible=showAllTasks?selected:selected.slice(0,5);taskHTML+=warning('tasks');if(!visible.length){taskHTML+='<p class="quiet">'+(status('tasks').state==='error'?'할 일을 불러오지 못했습니다. 연결 상태를 확인하세요.':'지금 처리할 할 일이 없습니다. 여유롭게 다음 작업을 시작하세요.')+'</p>'}else{taskHTML+=visible.map(t=>{const due=t.due&&(t.due<=dayKey());const tag=t.due?(t.due<dayKey()?'예정일 지남':t.due===dayKey()?'오늘 예정':t.due.slice(5)):t.list;return `<label class="task"><input type="checkbox" data-task="${esc(t.id)}" ${t.completed?'checked':''} ${busyTasks.has(t.id)||state.refreshing||['pending','unknown'].includes(t.mutationState)?'disabled':''}><span class="task-name">${esc(t.title)}${t.mutationState==='unknown'?' · 반영 여부 확인 중':''}</span><span class="tag ${due&&!t.completed?'urgent':''}" title="${esc(t.list)}">${esc(tag)}</span></label>`}).join('');if(!showAllTasks&&selected.length>5)taskHTML+=`<p class="subtle">외 ${selected.length-5}개 · 전체 보기에서 확인하세요.</p>`}}
 $('#task-section').innerHTML=taskHTML;
 const files=[];for(const source of ['drive','obsidian']){const f=state.files.find(f=>f.source===source);if(f)files.push(f)}for(const f of state.files){if(files.length>=2)break;if(!files.some(x=>x.id===f.id))files.push(f)}
 $('#recent-preview').innerHTML=files.length?files.map(fileCard).join(''):empty(state.refreshing?'최근 파일을 찾고 있어요.':'표시할 최근 파일이 없습니다.',!state.refreshing);
 const agents=[];for(const provider of ['codex','claude']){const a=state.agents.find(a=>a.provider===provider);if(a)agents.push(a)}$('#agents-preview').innerHTML=agents.length?agents.map(agentCard).join(''):empty(state.refreshing?'AI 작업 기록을 읽고 있어요.':'로컬 AI 작업 기록이 없습니다.');
}
function renderCalendar(){
 const list=$('#calendar-list');
 if(!state.settings.googleConnected){list.innerHTML=empty('Google 계정을 연결하면 앞으로 7일의 일정을 날짜별로 볼 수 있습니다.',true);return}
 const events=[...state.events].sort((a,b)=>a.start-b.start),now=Date.now()/1000;
 if(!events.length){list.innerHTML=warning('calendar')+empty(status('calendar').state==='error'?'일정을 불러오지 못했습니다. 연결 상태를 확인하세요.':'앞으로 7일간 표시할 일정이 없습니다.');return}
 const groups=[];
 for(const event of events){const stamp=calendarGroupStamp(event),key=dayKey(new Date(stamp*1000));let group=groups.find(item=>item.key===key);if(!group){group={key,stamp,events:[]};groups.push(group)}group.events.push(event)}
 list.innerHTML=warning('calendar')+groups.map(group=>`<section class="calendar-day"><h3>${esc(calendarDateLabel(group.stamp))}</h3><div class="calendar-events">${group.events.map(event=>`<button class="calendar-event ${event.end<=now?'past':''}" data-event="${esc(event.id)}"><span class="event-time">${esc(calendarTimeLabel(event))}</span><span class="event-copy"><strong>${esc(event.title)}</strong><small>${esc(event.calendar)}${window.orbitPlatform.hasMeeting(event)?` · ${icon('video')} 회의 링크`:''}</small></span>${icon('chevron')}</button>`).join('')}</div></section>`).join('')
}
function renderFiles(){const query=$('#file-search').value.normalize('NFC').trim().toLocaleLowerCase();const files=state.files.filter(f=>(fileSource==='all'||f.source===fileSource)&&`${f.name} ${f.parent}`.normalize('NFC').toLocaleLowerCase().includes(query));$('#file-list').innerHTML=files.length?files.map(fileCard).join(''):empty(query?'검색 결과가 없습니다.':'표시할 파일이 없습니다. 폴더 설정을 확인하세요.',!query);$('#file-description').textContent=`최근 수정 파일 ${files.length}개 · 폴더별 최대 30개${state.statuses.some(s=>['drive','obsidian'].includes(s.source)&&s.message.includes('일부'))?' · 일부 범위만 검색':''}`}
function renderAgents(){const agents=state.agents.filter(a=>agentProvider==='all'||a.provider===agentProvider);$('#agent-list').innerHTML=agents.length?agents.map(agentCard).join(''):empty('해당 서비스의 로컬 작업 기록이 없습니다.')}
function renderSettings(){const s=state.settings,launchAtLogin=window.orbitPlatform.launchAtLoginSupported(state),textSize=['normal','large','xlarge'].includes(s.textSize)?s.textSize:'normal';$$('.themes button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.theme===state.theme)));$$('.text-sizes button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.textSize===textSize)));$('#drive-path').textContent=s.drive||'선택된 폴더 없음';$('#vault-path').textContent=s.vault||'선택된 폴더 없음';$('#google-status').textContent=s.googleAuthorizing?'브라우저에서 Google 로그인을 완료하세요.':s.googleConnected?'Google 계정 연결됨':s.googleConfigured?'OAuth 설정 준비 완료 · Google 로그인이 필요합니다.':'첫 연결: 데스크톱 OAuth JSON을 가져오세요.';$('#google-connect').disabled=!s.googleConfigured||s.googleAuthorizing;$('#google-import').disabled=s.googleAuthorizing;$('#google-connect').textContent=s.googleConnected?'다시 연결':'Google 연결';$('#google-cancel').hidden=!s.googleAuthorizing;$('#google-disconnect').hidden=!s.googleConnected;$('#launch-login-option').hidden=!launchAtLogin;$('#launch-login').checked=launchAtLogin&&Boolean(s.launchAtLogin);$('#agent-roots').hidden=!state.capabilities?.chooseAgentRoots;$('#codex-path').textContent=s.codex||'선택된 폴더 없음';$('#claude-path').textContent=s.claude||'선택된 폴더 없음';$('#agent-open-help').textContent=window.orbitPlatform.agentHelp(state);$('#status-list').innerHTML=['calendar','tasks','drive','obsidian','codex','claude'].map(source=>{const s=status(source);return `<div class="connection-row"><div>${labels[source]}<small>${esc(s.message)}${s.updated?' · '+relative(s.updated):''}</small></div><span class="badge ${s.state==='error'?'error':''}">${s.state==='connected'?'연결됨':s.state==='error'?'확인 필요':'미연결'}</span></div>`}).join('')}
function tab(name){activeTab=name;$$('.tab').forEach(b=>{const on=b.dataset.tab===name;b.classList.toggle('active',on);b.setAttribute('aria-selected',String(on));b.tabIndex=on?0:-1});$$('[role=tabpanel]').forEach(s=>s.hidden=s.id!==name);$('#content').scrollTop=0}
function settings(show=true){$('#settings').hidden=!show;if(show)$('#settings-close').focus();else $('#settings-button').focus()}
document.addEventListener('click',async e=>{const button=e.target.closest('button');if(!button||button.disabled||button.getAttribute('aria-disabled')==='true')return;if(button.dataset.tab){tab(button.dataset.tab);return}if(button.dataset.file){act('openFile',{id:button.dataset.file});return}if(button.dataset.agent){act('openAgent',{id:button.dataset.agent});return}if(button.dataset.event){act('openEvent',{id:button.dataset.event,meeting:button.dataset.meeting==='true'});return}if(button.dataset.ddayOpen){openDdayManager();return}if(button.dataset.ddayEdit){openDdayManager(button.dataset.ddayEdit);return}if(button.dataset.ddayDelete){$(`[data-dday-confirm="${button.dataset.ddayDelete}"]`).hidden=false;return}if(button.dataset.ddayDeleteCancel){$(`[data-dday-confirm="${button.dataset.ddayDeleteCancel}"]`).hidden=true;return}if(button.dataset.ddayAction){await handleDdayAction(button);return}if(button.dataset.action){button.dataset.action==='settings'?settings():act(button.dataset.action);return}if(button.id==='all-tasks'){showAllTasks=!showAllTasks;renderToday(state.events.filter(e=>sameDay(e.start)));return}if(button.dataset.textSize){act('textSize',{value:button.dataset.textSize});return}if(button.dataset.theme){const theme=button.dataset.theme;document.body.dataset.theme=theme;act('theme',{value:theme});return}if(button.dataset.source){fileSource=button.dataset.source;$$('#file-filters button').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));renderFiles();return}if(button.dataset.provider){agentProvider=button.dataset.provider;$$('#agent-filters button').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));renderAgents()}});
document.addEventListener('change',async e=>{if(e.target.matches('[data-task]')){const id=e.target.dataset.task;busyTasks.add(id);e.target.disabled=true;await act('completeTask',{id,value:e.target.checked});busyTasks.delete(id);render()}if(e.target.id==='launch-login'&&window.orbitPlatform.launchAtLoginSupported(state)){await act('launchAtLogin',{value:e.target.checked});renderSettings()}});
$('#file-search').addEventListener('input',renderFiles);$('#settings-button').onclick=()=>settings();$('#settings-close').onclick=()=>settings(false);$('#dday-close').onclick=closeDdayManager;$('#dday-cancel').onclick=resetDdayForm;$('#dday-archived-toggle').onclick=()=>{showArchivedDdays=!showArchivedDdays;renderDdayManager()};$('#dday-form').addEventListener('submit',submitDday);$('#refresh').onclick=()=>act('refresh');$('#pin').onclick=()=>act('pin',{value:!state.pinned});$('#search-shortcut').onclick=()=>{tab('files');$('#file-search').focus()};
for(const [id,action] of Object.entries({'choose-drive':'chooseDrive','choose-vault':'chooseVault','choose-codex':'chooseCodex','choose-claude':'chooseClaude','google-import':'importGoogle','google-connect':'connectGoogle','google-cancel':'cancelGoogle','google-disconnect':'disconnectGoogle','google-help':'googleHelp','quit':'quit'})){$('#'+id).onclick=()=>act(action)}
document.addEventListener('keydown',e=>{if(trapDdayFocus(e))return;if((e.metaKey||e.ctrlKey)&&e.key.toLowerCase()==='k'){e.preventDefault();if(!$('#dday-manager').hidden)closeDdayManager();settings(false);tab('files');$('#file-search').focus()}if(e.key==='Escape'){if(!$('#dday-manager').hidden)closeDdayManager();else if(!$('#settings').hidden)settings(false);else act('close')}if(e.target.matches('.tab')&&['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const tabs=$$('.tab'),i=tabs.indexOf(e.target),next=e.key==='Home'?0:e.key==='End'?tabs.length-1:(i+(e.key==='ArrowRight'?1:tabs.length-1))%tabs.length;tab(tabs[next].dataset.tab);tabs[next].focus()}});
document.addEventListener('visibilitychange',refreshDdayClock);
window.addEventListener('focus',refreshDdayClock);
if(window.orbitPlatform?.canSend()){act('ready')}else{state.refreshing=false;state.capabilities={dday:true};render();const banner=document.createElement('div');banner.className='preview-banner';banner.textContent='Orbit 앱 리소스 미리보기 · 실제 데이터 연결은 Orbit 앱에서 작동합니다.';$('#app').prepend(banner)}
