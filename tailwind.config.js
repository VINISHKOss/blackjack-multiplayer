/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/BlackJack.Client/**/*.{razor,html,cshtml,cs}",
  ],
  theme: {
    extend: {
      fontFamily: {
        display: ["Cinzel", "Georgia", "serif"],
        sans: ["Inter", "system-ui", "sans-serif"],
      },
      colors: {
        casino: {
          felt: "#0d5c3a",
          dark: "#0a0f0d",
          muted: "#8ba89a",
          gold: "#d4af37",
        },
      },
      animation: {
        "fade-in": "fadeIn 0.4s ease-out forwards",
        "slide-up": "slideUp 0.5s ease-out forwards",
        deal: "dealCard 0.45s cubic-bezier(0.34, 1.56, 0.64, 1) forwards",
      },
      keyframes: {
        fadeIn: {
          "0%": { opacity: "0" },
          "100%": { opacity: "1" },
        },
        slideUp: {
          "0%": { opacity: "0", transform: "translateY(20px)" },
          "100%": { opacity: "1", transform: "translateY(0)" },
        },
        dealCard: {
          "0%": { opacity: "0", transform: "translateY(-60px) rotateY(90deg) scale(0.8)" },
          "100%": { opacity: "1", transform: "translateY(0) rotateY(0) scale(1)" },
        },
      },
    },
  },
  plugins: [],
};