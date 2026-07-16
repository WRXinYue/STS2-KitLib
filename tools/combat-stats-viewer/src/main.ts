import { createApp } from "vue";
import App from "./App.vue";
import { i18n } from "./i18n";
import "./main.css";

document.documentElement.classList.add("dark");

createApp(App).use(i18n).mount("#app");
