(() => {
  if (location.pathname !== "/" || document.querySelector(".cinema-splash")) return;

  const storageKey = "cineclay.splash.v3";
  try {
    if (sessionStorage.getItem(storageKey)) return;
  } catch {}

  const slides = [
    { industry: "Hollywood", mark: "H", type: "Movie", title: "Dune: Part Two", year: 2024, note: "Epic worlds. Monumental cinema.", video: "Way9Dexny3w", art: "posters/dune.jpg" },
    { industry: "Bollywood", mark: "ब", type: "Movie", title: "Jawan", year: 2023, note: "Scale, rhythm and a hero made for the big screen.", video: "COv52Qyctws", art: "https://images.herzindagi.info/image/2024/Jul/jawan-cast.jpg" },
    { industry: "Ollywood", mark: "ଓ", type: "Movie", title: "Pratikshya", year: 2022, note: "Stories rooted in Odisha, made for the world.", video: "E_p4kHzZBTM", art: "https://m.media-amazon.com/images/M/MV5BN2U0ZjJiNWUtM2M5Ni00ZDEzLWFlMTAtZWU2ZWQyZTY5MGYwXkEyXkFqcGc%40._V1_.jpg" },
    { industry: "Korean Cinema", mark: "한", type: "Movie", title: "Parasite", year: 2019, note: "Fearless storytelling without borders.", video: "5xH0HfJHsaY", art: "posters/parasite.jpg" },
    { industry: "Japanese Animation", mark: "ア", type: "Movie", title: "Spirited Away", year: 2001, note: "Hand-crafted wonder. A timeless journey.", video: "ByXuk9QqQkk", art: "posters/spirited.jpg" },
    { industry: "Global Television", mark: "TV", type: "Series", title: "Wednesday", year: 2022, note: "Distinctive worlds built one episode at a time.", video: "Di310WS8zLk", art: "https://static.tvmaze.com/uploads/images/original_untouched/586/1466410.jpg" },
    { industry: "Prestige Sci-Fi", mark: "S", type: "Series", title: "Severance", year: 2022, note: "Bold ideas. Meticulous television.", video: "_UXKlYvLGJY", art: "https://static.tvmaze.com/uploads/images/original_untouched/548/1371406.jpg" },
    { industry: "Indian Music", mark: "♪", type: "Album", title: "Odia Songs of 2022", year: 2022, note: "Regional voices, unforgettable melodies.", video: "I6Bs8xOwr4g", art: "https://i.ytimg.com/vi/I6Bs8xOwr4g/maxresdefault.jpg" }
  ];

  const overlay = document.createElement("section");
  overlay.className = "cinema-splash industry-splash showcase-splash";
  overlay.setAttribute("aria-label", "Cineclay industry showcase");
  overlay.innerHTML = `
    <div class="splash-backdrop" aria-hidden="true"></div>
    <div class="splash-video" aria-hidden="true"></div>
    <div class="splash-grain" aria-hidden="true"></div>
    <div class="splash-topline">
      <img class="splash-logo" src="logos/cineclay.svg" alt="Cineclay">
      <span>One screen · many storytelling worlds</span>
    </div>
    <div class="splash-showcase" aria-live="polite">
      <div class="industry-lockup"><span class="industry-emblem"></span><div><small>Curated industry spotlight</small><strong class="industry-name"></strong></div></div>
      <div class="splash-title-row"><span class="splash-media-type"></span><span class="splash-year"></span></div>
      <h1 class="splash-feature-title"></h1>
      <p class="industry-subtitle"></p>
      <div class="splash-actions">
        <button class="primary" id="enter-cinema">Enter Cineclay <span>↗</span></button>
        <a class="splash-trailer" id="splash-trailer" target="_blank" rel="noopener">▶ Watch spotlight</a>
      </div>
    </div>
    <div class="splash-rail" role="tablist" aria-label="Industry spotlights">
      ${slides.map((slide, index) => `<button role="tab" data-industry="${index}" aria-label="${slide.industry}: ${slide.title}"><span>${slide.mark}</span><div><strong>${slide.industry}</strong><small>${slide.title}</small></div></button>`).join("")}
    </div>
    <div class="splash-progress" aria-hidden="true"><i></i></div>
    <button class="skip-splash" id="skip-splash">Skip intro ×</button>`;

  document.body.append(overlay);
  document.body.classList.add("splash-open");
  const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches || navigator.connection?.saveData;
  let index = 0;
  let timer;

  function draw(next, userSelected = false) {
    index = (next + slides.length) % slides.length;
    const slide = slides[index];
    overlay.classList.remove("slide-ready");
    overlay.querySelector(".splash-backdrop").style.backgroundImage = `linear-gradient(90deg,rgba(5,12,9,.97) 8%,rgba(5,12,9,.63) 48%,rgba(5,12,9,.18)),url("${slide.art}")`;
    overlay.querySelector(".industry-emblem").textContent = slide.mark;
    overlay.querySelector(".industry-name").textContent = slide.industry;
    overlay.querySelector(".splash-media-type").textContent = slide.type;
    overlay.querySelector(".splash-year").textContent = slide.year;
    overlay.querySelector(".splash-feature-title").textContent = slide.title;
    overlay.querySelector(".industry-subtitle").textContent = slide.note;
    overlay.querySelector("#splash-trailer").href = `https://www.youtube.com/watch?v=${slide.video}`;
    overlay.querySelectorAll("[data-industry]").forEach((button, buttonIndex) => {
      const active = buttonIndex === index;
      button.classList.toggle("active", active);
      button.setAttribute("aria-selected", String(active));
      if (active && userSelected) button.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "nearest", inline: "center" });
    });
    overlay.querySelector(".splash-video").innerHTML = reduced ? "" : `<iframe title="${slide.industry} · ${slide.title} muted preview" src="https://www.youtube-nocookie.com/embed/${slide.video}?autoplay=1&mute=1&controls=0&playsinline=1&rel=0&modestbranding=1" allow="autoplay; encrypted-media" tabindex="-1"></iframe>`;
    const progress = overlay.querySelector(".splash-progress i");
    progress.style.animation = "none";
    void progress.offsetWidth;
    if (!reduced) progress.style.animation = "splashProgress 6s linear forwards";
    requestAnimationFrame(() => overlay.classList.add("slide-ready"));
  }

  function schedule() {
    clearInterval(timer);
    if (!reduced) timer = setInterval(() => draw(index + 1), 6000);
  }

  function close() {
    clearInterval(timer);
    try { sessionStorage.setItem(storageKey, "1"); } catch {}
    document.body.classList.remove("splash-open");
    overlay.querySelector(".splash-video").innerHTML = "";
    overlay.classList.add("leaving");
    setTimeout(() => overlay.remove(), reduced ? 0 : 650);
  }

  overlay.querySelector("#enter-cinema").addEventListener("click", close);
  overlay.querySelector("#skip-splash").addEventListener("click", close);
  overlay.querySelectorAll("[data-industry]").forEach(button => button.addEventListener("click", () => {
    draw(Number(button.dataset.industry), true);
    schedule();
  }));
  document.addEventListener("keydown", event => {
    if (!overlay.isConnected) return;
    if (event.key === "Escape") close();
    if (event.key === "ArrowRight") { draw(index + 1, true); schedule(); }
    if (event.key === "ArrowLeft") { draw(index - 1, true); schedule(); }
  });

  draw(0);
  schedule();
  overlay.querySelector("#enter-cinema").focus({ preventScroll: true });
})();
