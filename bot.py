```python
import asyncio
import os
from datetime import datetime

import requests
from aiogram import Bot, Dispatcher, F, types
from aiogram.filters import Command
from aiogram.fsm.context import FSMContext
from aiogram.fsm.state import State, StatesGroup
from aiogram.types import (
    InlineKeyboardButton,
    InlineKeyboardMarkup,
    KeyboardButton,
    ReplyKeyboardMarkup,
)
from dotenv import load_dotenv


load_dotenv()

TOKEN = os.getenv("TELEGRAM_TOKEN")
API_URL = "http://127.0.0.1:8000"

if not TOKEN:
    raise RuntimeError("TELEGRAM_TOKEN is not set in .env file")

bot = Bot(token=TOKEN)
dp = Dispatcher()


# FSM states

class RequestState(StatesGroup):
    waiting_for_anonymous_choice = State()
    waiting_for_request_text = State()


class RegistrationState(StatesGroup):
    waiting_for_full_name = State()
    waiting_for_group_name = State()
    waiting_for_phone = State()


# Keyboards

main_keyboard = ReplyKeyboardMarkup(
    keyboard=[
        [KeyboardButton(text="📰 Новини"), KeyboardButton(text="📅 Події")],
        [KeyboardButton(text="📝 Зареєструватися на подію")],
        [KeyboardButton(text="📨 Звернення"), KeyboardButton(text="⭐ Мої бали")]
    ],
    resize_keyboard=True
)

phone_keyboard = ReplyKeyboardMarkup(
    keyboard=[
        [
            KeyboardButton(
                text="📱 Надіслати номер телефону",
                request_contact=True
            )
        ]
    ],
    resize_keyboard=True,
    one_time_keyboard=True
)

# User registration

@dp.message(Command("start"))
async def start(message: types.Message, state: FSMContext):
    telegram_id = message.from_user.id

    response = requests.get(f"{API_URL}/users")

    if response.status_code == 200:
        users = response.json()

        existing_user = next(
            (user for user in users if user.get("telegram_id") == telegram_id),
            None
        )

        if existing_user:
            await message.answer(
                "Вітаю в StudGov Bot!\nОберіть потрібний розділ:",
                reply_markup=main_keyboard
            )
            return

    await state.set_state(RegistrationState.waiting_for_full_name)

    await message.answer(
        "Вітаю в StudGov Bot!\n\n"
        "Для подальшого користування системою необхідно пройти коротку реєстрацію.\n\n"
        "Ці дані потрібні для:\n"
        "• реєстрації на заходи;\n"
        "• нарахування балів активності;\n"
        "• коректної ідентифікації студента.\n\n"
        "Будь ласка, уважно вказуйте свої дані.\n\n"
        "Введіть ваше ПІБ:"
    )


@dp.message(RegistrationState.waiting_for_full_name)
async def process_full_name(message: types.Message, state: FSMContext):
    await state.update_data(full_name=message.text)
    await state.set_state(RegistrationState.waiting_for_group_name)

    await message.answer("Введіть вашу академічну групу:")


@dp.message(RegistrationState.waiting_for_group_name)
async def process_group_name(message: types.Message, state: FSMContext):
    await state.update_data(group_name=message.text)
    await state.set_state(RegistrationState.waiting_for_phone)

    await message.answer(
        "Натисніть кнопку нижче для надсилання номера телефону:",
        reply_markup=phone_keyboard
    )


@dp.message(RegistrationState.waiting_for_phone, F.contact)
async def process_phone(message: types.Message, state: FSMContext):
    data = await state.get_data()

    payload = {
        "telegram_id": message.from_user.id,
        "full_name": data["full_name"],
        "group_name": data["group_name"],
        "phone_number": message.contact.phone_number
    }

    response = requests.post(
        f"{API_URL}/telegram/register",
        json=payload
    )

    if response.status_code == 200:
        await message.answer(
            "✅ Реєстрація успішно завершена!\n\n"
            "Тепер ви можете користуватися системою.",
            reply_markup=main_keyboard
        )
    else:
        await message.answer(
            "❌ Помилка реєстрації.\n"
            "Спробуйте пізніше."
        )

    await state.clear()


# News

@dp.message(F.text == "📰 Новини")
async def news_button(message: types.Message):
    response = requests.get(f"{API_URL}/news")

    if response.status_code != 200:
        await message.answer("Помилка отримання новин")
        return

    news_list = response.json()

    if not news_list:
        await message.answer("Новин поки немає")
        return

    text = "📰 Останні новини:\n\n"

    for news in news_list[:5]:
        text += f"🔹 {news['title']}\n{news['body']}\n\n"

    await message.answer(text)


# Events

@dp.message(F.text == "📅 Події")
async def events_button(message: types.Message):
    response = requests.get(f"{API_URL}/events")

    if response.status_code != 200:
        await message.answer("Помилка отримання подій")
        return

    events = response.json()

    if not events:
        await message.answer("Подій поки немає")
        return

    text = "📅 Події:\n\n"

    for event in events[:10]:
        registration_text = (
            "потрібна"
            if event.get("requires_registration")
            else "не потрібна"
        )

        event_date = datetime.fromisoformat(event["starts_at"])
        formatted_date = event_date.strftime("%d.%m.%Y %H:%M")

        description = event.get("description") or "Опис не вказано"

        text += f"🔹 {event['title']}\n"
        text += f"📄 Опис: {description}\n"
        text += f"📍 {event.get('location') or 'Не вказано'}\n"
        text += f"🕒 {formatted_date}\n"
        text += f"📝 Реєстрація: {registration_text}\n\n"

    await message.answer(text)


# Event registration

@dp.message(F.text == "📝 Зареєструватися на подію")
async def show_registration_events(message: types.Message):
    response = requests.get(f"{API_URL}/events")

    if response.status_code != 200:
        await message.answer("Помилка отримання подій для реєстрації.")
        return

    events = response.json()

    registration_events = [
        event
        for event in events
        if event.get("requires_registration")
    ]

    if not registration_events:
        await message.answer(
            "Наразі немає подій, на які потрібна реєстрація."
        )
        return

    await message.answer("📝 Оберіть подію для реєстрації:")

    for event in registration_events[:10]:
        keyboard = InlineKeyboardMarkup(
            inline_keyboard=[
                [
                    InlineKeyboardButton(
                        text="🔥 Зареєструватися",
                        callback_data=f"register_event_{event['id']}"
                    )
                ]
            ]
        )

        event_date = datetime.fromisoformat(event["starts_at"])
        formatted_date = event_date.strftime("%d.%m.%Y %H:%M")

        text = (
            f"🔹 {event['title']}\n"
            f"📍 {event.get('location') or 'Не вказано'}\n"
            f"🕒 {formatted_date}"
        )

        await message.answer(text, reply_markup=keyboard)


@dp.callback_query(F.data.startswith("register_event_"))
async def confirm_event_registration(callback: types.CallbackQuery):
    event_id = callback.data.replace("register_event_", "")

    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text="✅ Так, зареєструватися",
                    callback_data=f"confirm_register_{event_id}"
                )
            ],
            [
                InlineKeyboardButton(
                    text="❌ Скасувати",
                    callback_data="cancel_register"
                )
            ]
        ]
    )

    await callback.message.answer(
        "Ви дійсно бажаєте зареєструватися на цю подію?",
        reply_markup=keyboard
    )

    await callback.answer()


@dp.callback_query(F.data.startswith("confirm_register_"))
async def process_confirm_registration(callback: types.CallbackQuery):
    event_id = int(callback.data.replace("confirm_register_", ""))
    telegram_id = callback.from_user.id

    user_response = requests.get(
        f"{API_URL}/users/by-telegram/{telegram_id}/activity"
    )

    if user_response.status_code != 200:
        await callback.message.answer(
            "❌ Помилка перевірки користувача."
        )
        await callback.answer()
        return

    user_data = user_response.json()

    if not user_data.get("found"):
        await callback.message.answer(
            "Ваш профіль не знайдено в системі.\n"
            "Спочатку пройдіть реєстрацію через /start."
        )
        await callback.answer()
        return

    response = requests.post(
        f"{API_URL}/events/{event_id}/register",
        json={"user_id": user_data["user_id"]}
    )

    if response.status_code == 200:
        await callback.message.answer(
            "✅ Ви успішно зареєструвалися на подію."
        )
    else:
        await callback.message.answer(
            "❌ Не вдалося зареєструватися.\n"
            "Можливо, ви вже зареєстровані або кількість місць вичерпано."
        )

    await callback.answer()


@dp.callback_query(F.data == "cancel_register")
async def cancel_registration(callback: types.CallbackQuery):
    await callback.message.answer("Реєстрацію скасовано.")
    await callback.answer()


# Requests

@dp.message(F.text == "📨 Звернення")
async def requests_button(message: types.Message, state: FSMContext):
    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text="👤 Від свого імені",
                    callback_data="request_not_anonymous"
                )
            ],
            [
                InlineKeyboardButton(
                    text="🕶 Анонімно",
                    callback_data="request_anonymous"
                )
            ]
        ]
    )

    await state.set_state(RequestState.waiting_for_anonymous_choice)

    await message.answer(
        "Оберіть спосіб подання звернення:\n\n"
        "👤 Якщо звернення подано від свого імені, ви отримаєте відповідь у цьому боті.\n"
        "🕶 Якщо звернення подано анонімно, відповідь у бот не надсилається, "
        "оскільки система не зберігає прив’язку до користувача.",
        reply_markup=keyboard
    )


@dp.callback_query(F.data.in_(["request_anonymous", "request_not_anonymous"]))
async def choose_request_type(callback: types.CallbackQuery, state: FSMContext):
    is_anonymous = callback.data == "request_anonymous"

    await state.update_data(is_anonymous=is_anonymous)
    await state.set_state(RequestState.waiting_for_request_text)

    await callback.message.answer(
        "Напишіть текст вашого звернення одним повідомленням."
    )

    await callback.answer()


@dp.message(RequestState.waiting_for_request_text)
async def process_request_text(message: types.Message, state: FSMContext):
    data = await state.get_data()
    is_anonymous = data.get("is_anonymous", True)
    user_id = None

    if not is_anonymous:
        telegram_id = message.from_user.id

        user_response = requests.get(
            f"{API_URL}/users/by-telegram/{telegram_id}/activity"
        )

        if user_response.status_code == 200:
            user_data = user_response.json()

            if user_data.get("found"):
                user_id = user_data["user_id"]

    payload = {
        "user_id": user_id,
        "is_anonymous": is_anonymous,
        "category": "Telegram звернення",
        "text": message.text
    }

    response = requests.post(f"{API_URL}/requests", json=payload)

    if response.status_code == 200:
        await message.answer(
            "✅ Ваше звернення успішно надіслано.",
            reply_markup=main_keyboard
        )
    else:
        await message.answer(
            "❌ Помилка під час надсилання звернення.",
            reply_markup=main_keyboard
        )

    await state.clear()


# Activity points

@dp.message(F.text == "⭐ Мої бали")
async def activity_button(message: types.Message):
    telegram_id = message.from_user.id

    response = requests.get(
        f"{API_URL}/users/by-telegram/{telegram_id}/activity"
    )

    if response.status_code != 200:
        await message.answer("❌ Помилка отримання балів.")
        return

    data = response.json()

    if not data.get("found"):
        await message.answer(
            "Ваш профіль поки не знайдено в системі.\n"
            "Спочатку пройдіть реєстрацію через /start."
        )
        return

    text = f"⭐ Ваші бали: {data['total_points']}\n\n"

    history = data.get("history", [])

    if not history:
        text += "Історія активності поки порожня."
    else:
        text += "Історія:\n"

        for item in history[:10]:
            reason = item.get("reason") or "Без опису"
            text += f"• {item['points']} бал(ів) — {reason}\n"

    await message.answer(text)


# Service commands

@dp.message(Command("myid"))
async def my_id(message: types.Message):
    await message.answer(f"Ваш Telegram ID: {message.from_user.id}")


async def main():
    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())

```
