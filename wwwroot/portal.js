'use strict';
(() => {
  const csrf = document.querySelector('meta[name="csrf"]')?.content;
  const note = document.querySelector('[data-note-token]');
  if (note) {
    const hash = location.hash.slice(1);
    if (/^[a-f0-9]{64}$/i.test(hash)) {
      sessionStorage.setItem('sirk-note', hash);
      history.replaceState(null, '', location.pathname);
    }
    const code = sessionStorage.getItem('sirk-note');
    if (code) document.querySelectorAll('[data-note-token]').forEach(x => { x.value = code; });
  }
  if (document.querySelector('#secret')) sessionStorage.removeItem('sirk-note');
  const map = {'/':'dashboard','/notes':'notes','/receive':'receive','/admin':'admin','/audit':'audit','/login':'login','/login-note':'login','/logout':'logout','/notes/new':'create','/notes/otp':'otp','/notes/read':'read','/notes/revoke':'revoke','/admin/revoke':'revoke','/admin/settings':'settings','/admin/link':'link','/admin/upload':'upload','/admin/toggle':'toggle','/admin/edit':'edit'};
  function log(type, target) {
    if (!csrf) return;
    const data = new URLSearchParams({type, target, __RequestVerificationToken: csrf});
    if (!navigator.sendBeacon('/audit/click', data)) fetch('/audit/click',{method:'POST',body:data,keepalive:true}).catch(()=>{});
  }
  document.addEventListener('click', async event => {
    const el = event.target.closest('a,button,input,select,textarea,label');
    if (!el) { log('other','other'); return; }
    if (el.dataset.copy) {
      log('copy',el.dataset.copy);
      const field = document.getElementById(el.dataset.copy);
      if (field) { try { await navigator.clipboard.writeText(field.value); } catch { field.select(); } }
      return;
    }
    if (el.tagName === 'A') {
      const path = new URL(el.href,location.origin).pathname;
      const resource = /^\/resource\/([a-f0-9]{64})$/i.exec(path);
      log(resource?'resource':'nav',resource?resource[1]:map[path]||'other');
    } else if (el.tagName === 'BUTTON') {
      log('submit',map[el.form?.getAttribute('action')]||'other');
    } else log('field','other');
  },true);
})();
