const dashboard = document.querySelector('#dashboardButton');
dashboard.addEventListener('click', () => {
  dashboard.textContent = 'Opening local dashboard…';
  window.setTimeout(() => { dashboard.textContent = 'Open installed dashboard'; }, 1800);
});
