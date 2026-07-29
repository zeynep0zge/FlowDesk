document.addEventListener("DOMContentLoaded", function () {
    const toggleButton = document.getElementById("sidebarToggle");
    const sidebar = document.getElementById("sidebarMenu");
    const backdrop = document.getElementById("sidebarBackdrop");
    const closeSidebar = function () {
        if (!sidebar) return;
        sidebar.classList.remove("show");
        toggleButton?.setAttribute("aria-expanded", "false");
        if (backdrop) backdrop.hidden = true;
    };
    if (toggleButton && sidebar) {
        toggleButton.setAttribute("aria-controls", "sidebarMenu");
        toggleButton.setAttribute("aria-expanded", "false");
        toggleButton.addEventListener("click", function () {
            const isOpen = sidebar.classList.toggle("show");
            toggleButton.setAttribute("aria-expanded", String(isOpen));
            if (backdrop) backdrop.hidden = !isOpen;
        });
        backdrop?.addEventListener("click", closeSidebar);
        document.addEventListener("keydown", event => {
            if (event.key === "Escape") closeSidebar();
        });
    }
    const normalizePath = path => path.toLowerCase().replace(/\/+$/, "") || "/";
    const currentPath = normalizePath(window.location.pathname);
    let activeLink = null;
    let bestMatchLength = -1;
    document.querySelectorAll(".sidebar-link, .header-nav-link").forEach(function (link) {
        const href = link.getAttribute("href");
        link.classList.remove("active");
        if (!href || href === "#") return;
        const linkPath = normalizePath(new URL(link.href).pathname);
        const matches = currentPath === linkPath || (linkPath !== "/" && currentPath.startsWith(linkPath + "/"));
        if (matches && linkPath.length > bestMatchLength) {
            activeLink = link;
            bestMatchLength = linkPath.length;
        }
    });
    activeLink?.classList.add("active");
});
