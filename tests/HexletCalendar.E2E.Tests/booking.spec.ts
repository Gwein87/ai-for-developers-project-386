import { test, expect } from '@playwright/test'

test('owner creates an event type, guest books a slot, owner sees the booking', async ({ browser }) => {
  const owner = await browser.newContext()
  const guest = await browser.newContext()
  const ownerPage = await owner.newPage()
  const guestPage = await guest.newPage()
  const eventName = `E2E call ${Date.now()}`
  const guestName = 'Browser Guest'
  const guestEmail = 'browser@example.test'

  await ownerPage.goto('/admin')
  await ownerPage.getByLabel('Название').fill(eventName)
  await ownerPage.getByLabel('Описание').fill('Browser test')
  await ownerPage.getByRole('button', { name: 'Создать тип события' }).click()
  await expect(ownerPage.getByRole('status')).toHaveText('Тип события создан')

  await guestPage.goto('/')
  await guestPage.getByRole('link', { name: 'Записаться' }).click()
  await guestPage.getByLabel(`Выбрать ${eventName}`).check()
  await guestPage.getByRole('button', { name: 'Продолжить' }).click()
  const dates = guestPage.getByRole('group', { name: 'Календарь' }).getByRole('button')
  await dates.nth(2).click()
  const times = guestPage.getByRole('group', { name: 'Время звонка' }).locator('button:not([disabled])')
  const selectedTime = await times.first().innerText()
  await times.first().click()
  await guestPage.getByRole('button', { name: 'Продолжить' }).click()
  await guestPage.getByLabel('Имя').fill(guestName)
  await guestPage.getByLabel('Email').fill(guestEmail)
  await guestPage.getByRole('button', { name: 'Подтвердить бронирование' }).click()
  await expect(guestPage.getByRole('heading', { name: 'Бронирование создано' })).toBeVisible()
  await expect(guestPage.getByText(selectedTime)).toBeVisible()
  await expect(guestPage.getByText(guestEmail)).toBeVisible()

  await ownerPage.reload()
  await ownerPage.getByRole('tab', { name: 'Бронирования' }).click()
  const booking = ownerPage.getByRole('listitem', { name: 'Предстоящее бронирование' })
  await expect(booking).toHaveCount(1)
  await expect(booking).toContainText(eventName)
  await expect(booking).toContainText(guestName)
  await expect(booking).toContainText(guestEmail)
  await guestPage.goto('/booking')
  await guestPage.getByLabel(`Выбрать ${eventName}`).check()
  await guestPage.getByRole('button', { name: 'Продолжить' }).click()
  await guestPage.getByRole('group', { name: 'Календарь' }).getByRole('button').nth(2).click()
  await expect(guestPage.getByRole('group', { name: 'Время звонка' }).getByRole('button', { name: selectedTime })).toBeDisabled()
  await owner.close()
  await guest.close()
})
