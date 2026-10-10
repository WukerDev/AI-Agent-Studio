<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { DateTime } from 'luxon'

const { t } = useI18n()
const router = useRouter()

const currentYear = DateTime.now().year

const features = [
  {
    icon: 'mdi-sitemap',
    titleKey: 'landing.features.visual.title',
    descKey: 'landing.features.visual.desc'
  },
  {
    icon: 'mdi-microsoft-windows',
    titleKey: 'landing.features.dotnet.title',
    descKey: 'landing.features.dotnet.desc'
  },
  {
    icon: 'mdi-brain',
    titleKey: 'landing.features.local.title',
    descKey: 'landing.features.local.desc'
  }
]

const navigateToEditor = (): void => {
  router.push({ name: 'Editor' })
}
</script>

<template>
  <v-app>
    <v-app-bar elevation="1">
      <v-app-bar-title>
        <span class="text-h6 font-weight-bold">{{ t('landing.title') }}</span>
      </v-app-bar-title>
      <v-spacer></v-spacer>
      <v-btn variant="text">{{ t('landing.nav.features') }}</v-btn>
      <v-btn variant="text">{{ t('landing.nav.pricing') }}</v-btn>
      <v-btn variant="outlined" class="mx-2">{{ t('landing.nav.login') }}</v-btn>
      <v-btn color="primary" variant="flat" @click="navigateToEditor">{{ t('landing.nav.start') }}</v-btn>
    </v-app-bar>

    <v-main>
      <v-container class="fill-height d-flex flex-column align-center justify-center py-16">
        <v-row class="w-100 text-center" align="center" justify="center">
          <v-col cols="12" md="8">
            <h1 class="text-h2 font-weight-black mb-6">
              {{ t('landing.hero.headline') }}
            </h1>
            <p class="text-h5 text-medium-emphasis mb-8">
              {{ t('landing.hero.subtitle') }}
            </p>
            <div class="d-flex gap-4 justify-center">
              <v-btn color="primary" size="x-large" @click="navigateToEditor">
                {{ t('landing.hero.cta') }}
              </v-btn>
              <v-btn variant="outlined" size="x-large" prepend-icon="mdi-github">
                {{ t('landing.hero.github') }}
              </v-btn>
            </div>
          </v-col>
        </v-row>
      </v-container>

      <v-container class="py-16">
        <h2 class="text-h3 text-center font-weight-bold mb-12">
          {{ t('landing.features.title') }}
        </h2>
        <v-row>
          <v-col cols="12" md="4" v-for="feature in features" :key="feature.titleKey">
            <v-card class="h-100 pa-6 text-center" elevation="0" border>
              <v-icon :icon="feature.icon" size="64" color="primary" class="mb-4"></v-icon>
              <h3 class="text-h5 font-weight-bold mb-2">{{ t(feature.titleKey) }}</h3>
              <p class="text-body-1 text-medium-emphasis">{{ t(feature.descKey) }}</p>
            </v-card>
          </v-col>
        </v-row>
      </v-container>
    </v-main>

    <v-footer border class="d-flex flex-column mt-auto">
      <div class="d-flex w-100 align-center px-4 py-2">
        <strong>{{ t('landing.title') }}</strong>
        <v-spacer></v-spacer>
        <span>&copy; {{ currentYear }} {{ t('landing.footer.rights') }}</span>
      </div>
    </v-footer>
  </v-app>
</template>