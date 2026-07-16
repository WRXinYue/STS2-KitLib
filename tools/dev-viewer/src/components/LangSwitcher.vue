<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { AppLocale } from "@/i18n";
import { setLocale } from "@/i18n";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

const { t, locale } = useI18n();

const current = computed({
  get: () => locale.value as AppLocale,
  set: (value: string) => setLocale(value as AppLocale),
});
</script>

<template>
  <Tabs v-model="current">
    <TabsList class="h-8">
      <TabsTrigger value="en" class="px-3 text-xs">
        {{ t("lang.en") }}
      </TabsTrigger>
      <TabsTrigger value="zh-CN" class="px-3 text-xs">
        {{ t("lang.zh-CN") }}
      </TabsTrigger>
    </TabsList>
  </Tabs>
</template>
