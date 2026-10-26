(async () => {
  const results = [];
  const check = (name, passed) => results.push({name, passed: Boolean(passed)});
  const now = Math.floor(Date.now()/1000);
  const base = {kind:'snapshot', platform:'macos', theme:'moss', pinned:false, refreshing:false,
    settings:{googleConnected:true,textSize:'normal'}, capabilities:{dday:true}, statuses:[], ddayError:'',
    files:['a','b','c'].map((id,index)=>({id:'file-'+id,name:'fixture-'+id+'.md',parent:'fixture',source:'obsidian',modified:now-index})),
    agents:[{id:'agent-a',provider:'codex',title:'fixture',project:'fixture',status:'최근 기록',updated:now}],
    tasks:['a','b'].map(id=>({id:'task-'+id,title:'fixture',completed:false,due:'',list:'fixture',mutationState:'idle'})),
    events:[{id:'event-a',title:'fixture',calendar:'fixture',start:now+600,end:now+3600,allDay:false}],
    ddays:[{id:'dday-a',title:'fixture',targetDate:'2030-01-01',pinned:false,archived:false,createdAt:'2026-01-01T00:00:00Z'}]};
  const apply = changes => window.orbit.receive({...base,...changes});
  const selected = (attribute,id) => document.activeElement?.getAttribute(attribute) === id;

  check('fixture web content is visible',!document.hidden);
  apply();tab('files');document.querySelector('#file-list [data-file="file-b"]').focus();
  check('file fixture starts with visible focused card',selected('data-file','file-b'));
  apply();check('same snapshot preserves file focus',selected('data-file','file-b'));
  document.querySelector('#file-list [data-file="file-b"]').focus();apply({files:[...base.files].reverse()});
  check('reordered snapshot preserves exact file focus',selected('data-file','file-b'));
  document.querySelector('#file-list [data-file="file-b"]').focus();apply({files:[base.files[0],base.files[2]]});
  check('removed file moves to adjacent file',selected('data-file','file-c'));
  document.querySelector('#file-list [data-file="file-c"]').focus();apply({files:[]});
  check('empty file list returns focus to search',document.activeElement.id==='file-search');

  apply();tab('agents');document.querySelector('#agent-list [data-agent]').focus();apply({agents:[{...base.agents[0],updated:now+20}]});
  check('changed agent snapshot preserves focus',selected('data-agent','agent-a'));
  tab('calendar');document.querySelector('#calendar-list [data-event]').focus();apply();
  check('calendar refresh preserves event focus',selected('data-event','event-a'));
  tab('today');document.querySelector('[data-dday-edit="dday-a"]').focus();refreshDdayClock();
  check('clock refresh preserves D-day focus',selected('data-dday-edit','dday-a'));

  const savedAct = act;let writes=0;act=async()=>{writes++;return true};
  apply();document.querySelector('#refresh').focus();apply({refreshing:true});
  check('refresh button remains focused while refreshing',document.activeElement.id==='refresh');
  document.querySelector('#refresh').click();await Promise.resolve();
  check('busy refresh cannot dispatch another refresh',writes===0);
  apply();document.querySelector('[data-task="task-a"]').focus();apply({refreshing:true});
  check('busy refresh preserves task focus',selected('data-task','task-a'));
  document.querySelector('[data-task="task-a"]').click();await Promise.resolve();
  check('busy task cannot dispatch a write',writes===0);
  for(const mutationState of ['pending','unknown']){
    apply({tasks:[{...base.tasks[0],mutationState}]});document.querySelector('[data-task]').click();await Promise.resolve();
    check(mutationState+' task cannot dispatch a write',writes===0);
  }
  apply();document.querySelector('[data-task="task-a"]').focus();document.querySelector('[data-task="task-a"]').click();
  await Promise.resolve();await Promise.resolve();
  check('unlocked task dispatches once and retains focus',writes===1&&selected('data-task','task-a'));
  act=savedAct;

  apply();settings(true,document.querySelector('#settings-button'));
  document.querySelector('#refresh').focus();
  check('settings blocks background focus',document.activeElement.id!=='refresh');
  const settingsButtons=Array.from(document.querySelectorAll('#settings button,#settings input')).filter(node=>!node.disabled&&!node.closest('[hidden]'));
  const first=settingsButtons[0],last=settingsButtons[settingsButtons.length-1];
  last.focus();document.dispatchEvent(new KeyboardEvent('keydown',{key:'Tab',bubbles:true,cancelable:true}));
  check('settings Tab wraps to first control',document.activeElement===first);
  first.focus();document.dispatchEvent(new KeyboardEvent('keydown',{key:'Tab',shiftKey:true,bubbles:true,cancelable:true}));
  check('settings Shift-Tab wraps to last control',document.activeElement===last);
  openDdayManager();closeDdayManager();document.querySelector('#refresh').focus();
  check('closing nested D-day keeps settings background locked',document.activeElement.id!=='refresh');
  settings(false);check('closing settings restores opener',document.activeElement.id==='settings-button');
  check('closing all dialogs releases background',!document.querySelector('#content').inert&&!document.querySelector('header').inert);
  apply();tab('today');const opener=document.querySelector('#connections button');opener.focus();settings(true,opener);apply();settings(false);
  check('settings restores replaced footer opener',Boolean(document.activeElement.closest('#connections')));
  apply({settings:{...base.settings,googleConfigured:true}});settings(true,document.querySelector('#settings-button'));document.querySelector('#google-connect').focus();
  apply({settings:{...base.settings,googleConfigured:true,googleAuthorizing:true}});
  check('disabled settings control moves focus inside dialog',Boolean(document.activeElement.closest('#settings'))&&!document.activeElement.disabled);
  settings(false);

  for(const platform of ['macos','windows'])for(const key of ['k','ㅏ','Process']){
    apply({platform});tab('today');settings(true,document.querySelector('#settings-button'));
    const event=new KeyboardEvent('keydown',{key,code:'KeyK',metaKey:platform==='macos',ctrlKey:platform==='windows',isComposing:key==='Process',bubbles:true,cancelable:true});
    document.dispatchEvent(event);
    check(platform+' '+key+' shortcut focuses search',event.defaultPrevented&&activeTab==='files'&&document.activeElement.id==='file-search'&&document.querySelector('#settings').hidden);
  }
  apply();tab('today');document.dispatchEvent(new KeyboardEvent('keydown',{key:'k',code:'KeyK',bubbles:true,cancelable:true}));
  check('plain K does not activate search',activeTab==='today');
  settings(true,document.querySelector('#settings-button'));document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true}));
  check('Escape closes settings and restores focus',document.querySelector('#settings').hidden&&document.activeElement.id==='settings-button');
  return results;
})()
