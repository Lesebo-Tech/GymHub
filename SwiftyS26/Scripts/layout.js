// ===================== MOBILE MENU TOGGLE =====================
function toggleMenu() {
    var menu = document.getElementById('mobileMenu');
    if (menu) menu.classList.toggle('open');
}

// ===================== NAVBAR SCROLL EFFECT =====================
window.addEventListener('scroll', function () {
    var nav = document.getElementById('navbar');
    if (!nav) return;
    if (window.scrollY > 50) {
        nav.style.background = 'rgba(10,22,40,0.98)';
        nav.style.boxShadow = '0 4px 24px rgba(0,0,0,0.3)';
    } else {
        nav.style.background = 'rgba(10,22,40,0.95)';
        nav.style.boxShadow = 'none';
    }
});

//// ===================== SIGN IN DROPDOWN =====================
//function toggleSignIn(e) {
//    // Stop the click from bubbling up to document
//    // so the "close on outside click" handler doesn't fire immediately
//    e.stopPropagation();

//    var menu = document.getElementById('signinMenu');
//    if (!menu) return;

//    // Toggle the open class
//    menu.classList.toggle('open');
//}

//// Close dropdown when clicking ANYWHERE outside it
//document.addEventListener('click', function (e) {
//    var menu = document.getElementById('signinMenu');
//    var toggle = document.querySelector('.dropdown-toggle');
//    if (!menu) return;

//    // Only close if the click was NOT inside the dropdown AND NOT on the toggle button
//    var clickedInsideMenu = menu.contains(e.target);
//    var clickedToggleBtn = toggle && toggle.contains(e.target);

//    if (!clickedInsideMenu && !clickedToggleBtn) {
//        menu.classList.remove('open');
//    }
//});

// ===================== SIGN IN DROPDOWN (FORCE FIX) =====================
window.onload = function () {

    var toggleBtn = document.querySelector('.dropdown-toggle');
    var menu = document.getElementById('signinMenu');

    if (!toggleBtn || !menu) {
        console.log("Dropdown elements not found");
        return;
    }

    toggleBtn.onclick = function (e) {
        e.stopPropagation();
        menu.classList.toggle('open');
        console.log("Toggle clicked"); // debug
    };

    document.onclick = function (e) {
        if (!menu.contains(e.target) && !toggleBtn.contains(e.target)) {
            menu.classList.remove('open');
        }
    };
};

// ===================== SMOOTH SCROLL =====================
document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
    anchor.addEventListener('click', function (e) {
        var href = this.getAttribute('href');

        // Skip if it's just '#' with nothing after it
        if (!href || href === '#') return;

        var target = document.querySelector(href);
        if (target) {
            e.preventDefault();
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    });
});

// ===================== SCROLL REVEAL ANIMATION =====================
var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
        if (entry.isIntersecting) {
            entry.target.style.opacity = '1';
            entry.target.style.transform = 'translateY(0)';
        }
    });
}, { threshold: 0.1 });

document.querySelectorAll('.plan-card, .class-card, .contact-form').forEach(function (el) {
    el.style.opacity = '0';
    el.style.transform = 'translateY(30px)';
    el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
    observer.observe(el);
});

// ===================== GO TO REGISTER PAGE =====================
function goToRegister() {
    var planSelect = document.getElementById('planSelect');
    var plan = planSelect ? planSelect.value : 'standard';
    window.location.href = '/Forms/Register?plan=' + plan;
}

/* ====================================================
   HERO SLIDESHOW
==================================================== */
let currentSlide = 0;
const slides = document.querySelectorAll('.hero-slide');
const dots = document.querySelectorAll('.dot');

function goToSlide(n) {
    slides[currentSlide].classList.remove('active');
    dots[currentSlide].classList.remove('active');
    currentSlide = n;
    slides[currentSlide].classList.add('active');
    dots[currentSlide].classList.add('active');
}

function nextSlide() {
    let next = (currentSlide + 1) % slides.length;
    goToSlide(next);
}

// Auto-play every 5 seconds
if (slides.length > 0) {
    setInterval(nextSlide, 5000);
}