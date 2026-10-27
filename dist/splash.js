(() => {
  if (location.pathname !== "/" || document.querySelector(".cinema-splash")) return;

  const storageKey = "cineclay.splash.v4";
  try {
    if (sessionStorage.getItem(storageKey)) return;
  } catch {}

  const slides = [
    { industry: "Hollywood", region: "American cinema", mark: "H", type: "Movie", title: "Dune: Part Two", year: 2024, note: "Epic worlds. Monumental cinema.", video: "Way9Dexny3w", art: "posters/dune.jpg" },
    { industry: "Bollywood", region: "Hindi cinema", mark: "ब", type: "Movie", title: "Jawan", year: 2023, note: "Scale, rhythm and a hero made for the big screen.", video: "COv52Qyctws", art: "https://images.herzindagi.info/image/2024/Jul/jawan-cast.jpg" },
    { industry: "Ollywood", region: "Odia cinema", mark: "ଓ", type: "Movie", title: "DAMaN", year: 2022, note: "Stories rooted in Odisha, made for the world.", video: "MOckS35r8yE", art: "https://images.justwatch.com/poster/297596886/s718/daman.jpg" },
    { industry: "Tollywood", region: "Telugu cinema", mark: "తె", type: "Movie", title: "RRR", year: 2022, note: "Mythic action and Telugu spectacle on a global stage.", video: "NgBoMJy386M", art: "https://i.ytimg.com/vi/NgBoMJy386M/maxresdefault.jpg" },
    { industry: "Kollywood", region: "Tamil cinema", mark: "த", type: "Movie", title: "Vikram", year: 2022, note: "Tamil cinema with pulse, scale and fearless craft.", video: "OKBMCL-frPU", art: "https://i.ytimg.com/vi/OKBMCL-frPU/maxresdefault.jpg" },
    { industry: "Mollywood", region: "Malayalam cinema", mark: "മ", type: "Movie", title: "Manjummel Boys", year: 2024, note: "Human stories and meticulous Malayalam filmmaking.", video: "UM7PPfz8LQU", art: "https://i.ytimg.com/vi/UM7PPfz8LQU/maxresdefault.jpg" },
    { industry: "Sandalwood", region: "Kannada cinema", mark: "ಕ", type: "Movie", title: "Kantara", year: 2022, note: "Kannada folklore, land and belief in cinematic form.", video: "8mrVmf239GU", art: "https://i.ytimg.com/vi/8mrVmf239GU/maxresdefault.jpg" },
    { industry: "Korean Cinema", region: "South Korean cinema", mark: "한", type: "Movie", title: "Parasite", year: 2019, note: "Fearless storytelling without borders.", video: "5xH0HfJHsaY", art: "posters/parasite.jpg" },
    { industry: "Japanese Cinema", region: "Japanese animation", mark: "ア", type: "Movie", title: "Spirited Away", year: 2001, note: "Hand-crafted wonder. A timeless journey.", video: "ByXuk9QqQkk", art: "posters/spirited.jpg" }
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
      ${slides.map((slide, index) => `<button role="tab" data-industry="${index}" aria-label="${slide.industry}, ${slide.region}: ${slide.title}"><span>${slide.mark}</span><div><strong>${slide.industry}</strong><small>${slide.region}</small></div></button>`).join("")}
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
